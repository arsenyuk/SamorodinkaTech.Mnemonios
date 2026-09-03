using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Entities;
using Mnemonios.Domain.Enums;
using Mnemonios.Domain.Interfaces;
using Mnemonios.Domain.Validation;
using Mnemonios.Infrastructure.Persistence;

namespace Mnemonios.Infrastructure.Services;

/// <summary>
/// Service for resolving (identifying) persons in the ЕДИН MPI.
/// </summary>
public class PersonResolveService : IPersonResolveService
{
    private const int DefaultNormalizationVersion = 1;

    private readonly IPersonRepository _repository;
    private readonly INormalizationService _normalizationService;
    private readonly IIdentificationKeyService _keyService;
    private readonly IPersonCessationService _cessationService;
    private readonly IPersonMergeService _mergeService;
    private readonly AppDbContext _context;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="PersonResolveService"/>.
    /// </summary>
    public PersonResolveService(
        IPersonRepository repository,
        INormalizationService normalizationService,
        IIdentificationKeyService keyService,
        IPersonCessationService cessationService,
        IPersonMergeService mergeService,
        AppDbContext context)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _normalizationService = normalizationService ?? throw new ArgumentNullException(nameof(normalizationService));
        _keyService = keyService ?? throw new ArgumentNullException(nameof(keyService));
        _cessationService = cessationService ?? throw new ArgumentNullException(nameof(cessationService));
        _mergeService = mergeService ?? throw new ArgumentNullException(nameof(mergeService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task<ResolveResponse> ResolveAsync(
        ResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = PersonResolveValidator.Validate(request);
        if (!validation.IsValid)
            throw new ArgumentException($"Validation failed: {string.Join("; ", validation.Errors)}");

        var defects = PersonResolveValidator.ValidateDefects(request);

        // --- Staging: create ext_persons record from raw incoming data ---
        var extPerson = CreateExtPersonEntity(request);
        var extDefects = CreateExtDefectEntities(extPerson.Id, defects, request);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.ExtPersons.Add(extPerson);
            await _context.SaveChangesAsync(cancellationToken);

            if (extDefects.Count > 0)
            {
                _context.ExtPersonDefects.AddRange(extDefects);
                await _context.SaveChangesAsync(cancellationToken);
            }

            // --- Process golden records ---
            var response = await ResolveByMatchingAsync(
                request, defects, extPerson, cancellationToken);

            // --- Mark staging record as processed ---
            var goldenMasterId = response.MasterId;
            await _repository.MarkExtPersonProcessedAsync(extPerson.Id, goldenMasterId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<ResolveResponse> ResolveByMatchingAsync(
        ResolveRequest request,
        IReadOnlyList<DefectInfo> defects,
        ExtPerson extPerson,
        CancellationToken cancellationToken)
    {
        var computedKeys = _keyService.ComputeKeys(request, DefaultNormalizationVersion);

        // Все ключи для матчинга: proof + составные ФИО
        var allMatchingKeys = computedKeys.Where(k => k.KeyType is "inn" or "snils" or "dul" or "inn_fio" or "snils_fio" or "dul_fio").ToList();
        var matchingKeyValues = allMatchingKeys.Select(k => k.KeyValue);

        // 1. Найти всех кандидатов по любому ключу
        var candidateIds = await _repository.FindPersonIdsByKeysAsync(matchingKeyValues, cancellationToken);

        if (candidateIds.Count == 0)
        {
            // Unmatched — создаём нового
            return await CreateNewPersonAsync(request, defects, extPerson, computedKeys, cancellationToken);
        }

        // 2. Для каждого кандидата посчитать M (совпадений), K (в мастере, не совпадают)
        var candidateScores = new List<(Guid MasterId, int M, int K, List<KeyConflict> Conflicts)>();

        foreach (var masterId in candidateIds)
        {
            var existingKeys = await _context.PersonIdentificationKeys
                .Where(k => k.MasterId == masterId)
                .ToListAsync(cancellationToken);

            int m = 0, k = 0;
            var conflicts = new List<KeyConflict>();

            foreach (var requestKey in allMatchingKeys)
            {
                var existing = existingKeys.FirstOrDefault(e => e.KeyType == requestKey.KeyType);
                if (existing is null)
                {
                    // Ключа нет в мастере — новые данные, OK
                    continue;
                }

                if (existing.KeyValue == requestKey.KeyValue)
                {
                    m++;
                }
                else
                {
                    k++;
                    conflicts.Add(new KeyConflict(requestKey.KeyType));
                }
            }

            candidateScores.Add((masterId, m, k, conflicts));
        }

        // 3. Выбрать кандидата с максимальным M, при равенстве — минимальный K
        var best = candidateScores
            .OrderByDescending(c => c.M)
            .ThenBy(c => c.K)
            .First();

        // 4. Проверить результат
        if (best.K > 0)
        {
            // Есть ключи в мастере которые не совпадают → Ambiguous
            return await HandleAmbiguousAsync(
                best.MasterId, best.Conflicts, request, extPerson, defects, computedKeys, cancellationToken);
        }

        if (best.M == 0)
        {
            // Все ключи новые (нет совпадений с мастером) → Unmatched
            return await CreateNewPersonAsync(request, defects, extPerson, computedKeys, cancellationToken);
        }

        // M > 0, K = 0 → Matched
        var masterIdResult = best.MasterId;

        // Обогатить голд-запись
        await EnrichPersonAsync(masterIdResult, request, cancellationToken);

        // Добавить новые ключи идентификации
        await SaveNewKeysAsync(masterIdResult, computedKeys, cancellationToken);

        await LinkExternalIdAsync(masterIdResult, request, extPerson.Id, cancellationToken);

        await SaveDocumentAsync(masterIdResult, request, computedKeys, cancellationToken);

        if (defects.Count > 0)
        {
            await SaveDefectsAsync(masterIdResult, defects, request, cancellationToken);
        }

        await _cessationService.CancelDeferredCessationAsync(
            request.SourceSystemId, request.ExternalPersonId, cancellationToken);

        var pendingCessationMatched = await _repository.GetPendingDeferredCessationAsync(
            request.SourceSystemId, request.ExternalPersonId, cancellationToken);

        return new ResolveResponse
        {
            Status = PersonMatchStatus.Matched,
            MasterId = masterIdResult,
            HasDefects = defects.Count > 0,
            Defects = defects,
            ScheduledDeletionDate = pendingCessationMatched?.ScheduledDeletionDate
        };
    }

    private async Task<ResolveResponse> CreateNewPersonAsync(
        ResolveRequest request,
        IReadOnlyList<DefectInfo> defects,
        ExtPerson extPerson,
        IReadOnlyList<IdentificationKey> computedKeys,
        CancellationToken cancellationToken)
    {
        var newMasterId = Guid.NewGuid();
        var person = CreatePersonEntity(newMasterId);
        var identificationKeys = CreateKeyEntities(person.MasterId, computedKeys, request.OrganizationUnitKey);
        var externalId = CreateExternalIdEntity(person.MasterId, request, extPerson.Id);

        var created = await _repository.CreateAsync(person, identificationKeys, externalId, cancellationToken);

        await SaveDocumentAsync(created.MasterId, request, computedKeys, cancellationToken);

        if (defects.Count > 0)
        {
            await SaveDefectsAsync(created.MasterId, defects, request, cancellationToken);
        }

        await _cessationService.CancelDeferredCessationAsync(
            request.SourceSystemId, request.ExternalPersonId, cancellationToken);

        return new ResolveResponse
        {
            Status = PersonMatchStatus.Unmatched,
            MasterId = created.MasterId,
            HasDefects = defects.Count > 0,
            Defects = defects
        };
    }

    private async Task LinkExternalIdAsync(
        Guid masterId,
        ResolveRequest request,
        Guid extPersonId,
        CancellationToken cancellationToken)
    {
        var externalId = CreateExternalIdEntity(masterId, request, extPersonId);

        var (updated, _) = await _repository.TryUpdateExternalIdAsync(externalId, cancellationToken);
        if (!updated)
        {
            await _repository.AddExternalIdAsync(externalId, cancellationToken);
        }
    }

    private async Task SaveDefectsAsync(
        Guid masterId,
        IReadOnlyList<DefectInfo> defects,
        ResolveRequest request,
        CancellationToken cancellationToken)
    {
        var entityDefects = defects.Select(d => new PersonDefect
        {
            Id = Guid.NewGuid(),
            MasterId = masterId,
            DefectType = d.DefectType,
            DefectMessage = d.DefectMessage,
            FieldName = d.FieldName,
            OriginalValue = GetOriginalValue(d, request),
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _repository.SaveDefectsAsync(entityDefects, cancellationToken);
    }

    private static string? GetOriginalValue(DefectInfo defect, ResolveRequest request)
    {
        var evidence = request.Evidence;
        return defect.DefectType switch
        {
            "invalid_inn" => evidence?.Inn,
            "invalid_snils" => evidence?.Snils,
            "dul_incomplete" when defect.FieldName == "dulNumber" => evidence?.DulSeries,
            "dul_incomplete" when defect.FieldName == "dulSeries" => evidence?.DulNumber,
            _ => null
        };
    }

    private static ExtPerson CreateExtPersonEntity(ResolveRequest request)
    {
        var rawEvidence = request.Evidence is not null
            ? JsonSerializer.Serialize(request.Evidence)
            : null;

        return new ExtPerson
        {
            Id = Guid.NewGuid(),
            SourceSystemId = request.SourceSystemId,
            ExternalPersonId = request.ExternalPersonId,
            ExternalPersonType = request.ExternalPersonType,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            RawEvidence = rawEvidence,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static IReadOnlyList<ExtPersonDefect> CreateExtDefectEntities(
        Guid extPersonId,
        IReadOnlyList<DefectInfo> defects,
        ResolveRequest request)
    {
        return defects.Select(d => new ExtPersonDefect
        {
            Id = Guid.NewGuid(),
            ExtPersonId = extPersonId,
            DefectType = d.DefectType,
            DefectMessage = d.DefectMessage,
            FieldName = d.FieldName,
            OriginalValue = GetOriginalValue(d, request),
            CreatedAt = DateTime.UtcNow
        }).ToList();
    }

    private Person CreatePersonEntity(Guid masterId)
    {
        var now = DateTime.UtcNow;

        return new Person
        {
            MasterId = masterId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static IReadOnlyList<PersonIdentificationKey> CreateKeyEntities(
        Guid masterId,
        IReadOnlyList<IdentificationKey> computedKeys,
        string organizationUnitKey)
    {
        var now = DateTime.UtcNow;
        return computedKeys.Select(k => new PersonIdentificationKey
        {
            Id = Guid.NewGuid(),
            MasterId = masterId,
            KeyType = k.KeyType,
            KeyValue = k.KeyValue,
            NormalizationVersion = DefaultNormalizationVersion,
            OrganizationUnitKey = organizationUnitKey,
            CreatedAt = now
        }).ToList();
    }

    private static PersonExternalId CreateExternalIdEntity(Guid masterId, ResolveRequest request, Guid extPersonId)
    {
        var now = DateTime.UtcNow;
        return new PersonExternalId
        {
            Id = Guid.NewGuid(),
            MasterId = masterId,
            SourceSystemId = request.SourceSystemId,
            ExternalPersonId = request.ExternalPersonId,
            ExternalPersonType = request.ExternalPersonType,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private async Task EnrichPersonAsync(
        Guid masterId,
        ResolveRequest request,
        CancellationToken cancellationToken)
    {
        var person = await _repository.GetByIdAsync(masterId, cancellationToken);
        if (person is null)
            return;

        var now = DateTime.UtcNow;
        person.UpdatedAt = now;
        await _repository.GetByIdAsync(masterId, cancellationToken);
        _context.Persons.Update(person);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveDocumentAsync(
        Guid masterId,
        ResolveRequest request,
        IReadOnlyList<IdentificationKey> computedKeys,
        CancellationToken cancellationToken)
    {
        var evidence = request.Evidence;
        if (string.IsNullOrWhiteSpace(evidence?.DulSeries) ||
            string.IsNullOrWhiteSpace(evidence?.DulNumber))
            return;

        // Использовать HMAC-хеш ДУЛ из вычисленных ключей
        var dulKey = computedKeys.FirstOrDefault(k => k.KeyType == "dul");
        if (dulKey is null)
            return;

        var documentHash = dulKey.KeyValue;

        // Проверить дубликат (уникальный индекс person_id + document_hash)
        var exists = await _context.PersonDocuments
            .AnyAsync(d => d.MasterId == masterId && d.DocumentHash == documentHash, cancellationToken);
        if (exists)
            return;

        var doc = new PersonDocument
        {
            Id = Guid.NewGuid(),
            MasterId = masterId,
            DocumentType = evidence.DulType ?? string.Empty,
            DocumentHash = documentHash,
            CreatedAt = DateTime.UtcNow
        };
        _context.PersonDocuments.Add(doc);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveNewKeysAsync(
        Guid masterId,
        IReadOnlyList<IdentificationKey> computedKeys,
        CancellationToken cancellationToken)
    {
        // Получить существующие ключи лица
        var existingKeyValues = await _context.PersonIdentificationKeys
            .Where(k => k.MasterId == masterId)
            .Select(k => k.KeyValue)
            .ToListAsync(cancellationToken);

        // Добавить только новые ключи (пропуск дубликатов)
        var now = DateTime.UtcNow;
        foreach (var key in computedKeys)
        {
            if (existingKeyValues.Contains(key.KeyValue))
                continue;

            _context.PersonIdentificationKeys.Add(new PersonIdentificationKey
            {
                Id = Guid.NewGuid(),
                MasterId = masterId,
                KeyType = key.KeyType,
                KeyValue = key.KeyValue,
                NormalizationVersion = DefaultNormalizationVersion,
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<ResolveResponse> HandleAmbiguousAsync(
        Guid existingMasterId,
        List<KeyConflict> conflicts,
        ResolveRequest request,
        ExtPerson extPerson,
        IReadOnlyList<DefectInfo> defects,
        IReadOnlyList<IdentificationKey> computedKeys,
        CancellationToken cancellationToken)
    {
        // Создать новую мастер-запись со ВСЕМИ ключами (уникальный индекс теперь по person_id)
        var newMasterId = Guid.NewGuid();
        var person = CreatePersonEntity(newMasterId);
        var identificationKeys = CreateKeyEntities(newMasterId, computedKeys, request.OrganizationUnitKey);

        // Проверить: внешняя ссылка уже существует?
        var existingExtId = await _context.PersonExternalIds
            .FirstOrDefaultAsync(e =>
                e.SourceSystemId == request.SourceSystemId &&
                e.ExternalPersonId == request.ExternalPersonId, cancellationToken);

        if (existingExtId is not null)
        {
            // Ссылка уже есть — просто привязать к новой персоне
            existingExtId.MasterId = newMasterId;
            existingExtId.UpdatedAt = DateTime.UtcNow;
            _context.Persons.Add(person);
            foreach (var key in identificationKeys)
            {
                key.MasterId = newMasterId;
                _context.PersonIdentificationKeys.Add(key);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var externalId = CreateExternalIdEntity(newMasterId, request, extPerson.Id);
            await _repository.CreateAsync(person, identificationKeys, externalId, cancellationToken);
        }

        await SaveDocumentAsync(newMasterId, request, computedKeys, cancellationToken);

        if (defects.Count > 0)
        {
            await SaveDefectsAsync(newMasterId, defects, request, cancellationToken);
        }

        await _cessationService.CancelDeferredCessationAsync(
            request.SourceSystemId, request.ExternalPersonId, cancellationToken);

        // Записать в очередь на обработку стюардом
        var sharedKey = computedKeys.FirstOrDefault(k => k.KeyType is "inn" or "snils" or "dul");
        var review = new PersonReviewQueue
        {
            Id = Guid.NewGuid(),
            PersonAId = existingMasterId,
            PersonBId = newMasterId,
            SharedKeyType = sharedKey?.KeyType ?? string.Empty,
            ConflictKeyType = string.Join(",", conflicts.Select(c => c.KeyType)),
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        _context.PersonReviewQueues.Add(review);
        await _context.SaveChangesAsync(cancellationToken);

        return new ResolveResponse
        {
            Status = PersonMatchStatus.Ambiguous,
            MasterId = newMasterId,
            HasDefects = defects.Count > 0,
            Defects = defects,
            KeyConflicts = conflicts
        };
    }
}
