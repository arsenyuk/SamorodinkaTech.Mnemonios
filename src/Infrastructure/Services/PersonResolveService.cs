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
        var computedKeys = _keyService.ComputeKeys(request, DefaultNormalizationVersion);

        // --- Staging: create ext_persons record with hashes ---
        var extPerson = CreateExtPersonEntity(request, computedKeys);
        var extDefects = CreateExtDefectEntities(extPerson.Id, defects);

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
                request, defects, extPerson, computedKeys, cancellationToken);

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
        IReadOnlyList<IdentificationKey> computedKeys,
        CancellationToken cancellationToken)
    {

        // 0. Проверить существующую внешнюю ссылку — если внешний ID уже привязан
        //    к персоне, это та же самая персона (вне зависимости от составных ключей ФИО)
        var existingExtId = await _context.PersonExternalIds
            .FirstOrDefaultAsync(e =>
                e.SourceSystemId == request.SourceSystemId &&
                e.ExternalPersonId == request.ExternalPersonId, cancellationToken);

        if (existingExtId is not null)
        {
            var matchedMasterId = existingExtId.MasterId;

            await EnrichPersonAsync(matchedMasterId, request, cancellationToken);
            await SaveNewKeysAsync(matchedMasterId, computedKeys, cancellationToken);
            await LinkExternalIdAsync(matchedMasterId, request, extPerson.Id, cancellationToken);
            await SaveDocumentAsync(matchedMasterId, request, computedKeys, cancellationToken);

            if (defects.Count > 0)
                await SaveDefectsAsync(matchedMasterId, defects, cancellationToken);

            await _cessationService.CancelDeferredCessationAsync(
                request.SourceSystemId, request.ExternalPersonId, cancellationToken);

            var pendingCessation = await _repository.GetPendingDeferredCessationAsync(
                request.SourceSystemId, request.ExternalPersonId, cancellationToken);

            return new ResolveResponse
            {
                Status = PersonMatchStatus.Matched,
                MasterId = matchedMasterId,
                HasDefects = defects.Count > 0,
                Defects = defects,
                ScheduledDeletionDate = pendingCessation?.ScheduledDeletionDate
            };
        }

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
        var candidateScores = new List<(Guid MasterId, int M, int K, int D, List<KeyConflict> ScoringConflicts, List<KeyConflict> ReportConflicts, string BestMatchedProofKey)>();

        foreach (var masterId in candidateIds)
        {
            var existingKeys = await _context.PersonIdentificationKeys
                .Where(k => k.MasterId == masterId)
                .ToListAsync(cancellationToken);

            int m = 0, k = 0, d = 0;
            var scoringConflicts = new List<KeyConflict>();
            var reportConflicts = new List<KeyConflict>();

            // Сначала оценить proof-ключи (inn, snils, dul), затем составные (inn_fio, ...)
            var proofKeys = allMatchingKeys.Where(k => k.KeyType is "inn" or "snils" or "dul").ToList();
            var compositeKeys = allMatchingKeys.Where(k => k.KeyType is "inn_fio" or "snils_fio" or "dul_fio").ToList();

            // Набор proof-ключей, которые уже совпали с мастером
            var matchedProofTypes = new HashSet<string>();

            foreach (var requestKey in proofKeys)
            {
                var existing = existingKeys.FirstOrDefault(e => e.KeyType == requestKey.KeyType);
                if (existing is null) continue;

                if (existing.KeyValue == requestKey.KeyValue)
                {
                    m++;
                    d += DeterministicKeyWeight(requestKey.KeyType);
                    matchedProofTypes.Add(requestKey.KeyType);
                }
                else
                {
                    k++;
                    scoringConflicts.Add(new KeyConflict(requestKey.KeyType));
                    reportConflicts.Add(new KeyConflict(requestKey.KeyType));
                }
            }

            // Составные ключи: scoring — конфликт только если proof-ключ НЕ совпал;
            // reporting — показываем все расхождения для информации стюарда
            foreach (var requestKey in compositeKeys)
            {
                var existing = existingKeys.FirstOrDefault(e => e.KeyType == requestKey.KeyType);
                if (existing is null) continue;

                // Соответствие proof-ключа: inn_fio → inn, snils_fio → snils, dul_fio → dul
                var baseProofType = requestKey.KeyType.Replace("_fio", "");

                if (existing.KeyValue == requestKey.KeyValue)
                {
                    m++;
                }
                else
                {
                    reportConflicts.Add(new KeyConflict(requestKey.KeyType));

                    if (!matchedProofTypes.Contains(baseProofType))
                    {
                        // Scoring: конфликт составного ключа засчитывается только если proof-ключ не совпал
                        k++;
                        scoringConflicts.Add(new KeyConflict(requestKey.KeyType));
                    }
                    // Если proof-ключ совпал — изменение ФИО ожидаемо, для scoring не засчитываем
                }
            }

            // Лучший совпавший proof-ключ (приоритет: ИНН > СНИЛС > ДУЛ)
            var bestMatched = matchedProofTypes
                .OrderByDescending(t => DeterministicKeyWeight(t))
                .FirstOrDefault() ?? string.Empty;

            candidateScores.Add((masterId, m, k, d, scoringConflicts, reportConflicts, bestMatched));
        }

        // 3. Выбрать кандидата: min K → max D → max M
        var best = candidateScores
            .OrderBy(c => c.K)
            .ThenByDescending(c => c.D)
            .ThenByDescending(c => c.M)
            .First();

        // 4. Проверить результат
        if (best.K > 0)
        {
            // Лучший кандидат → Ambiguous (создаёт нового person + запись в очереди)
            var result = await HandleAmbiguousAsync(
                best.MasterId, best.BestMatchedProofKey, best.ReportConflicts, request, extPerson, defects, computedKeys, cancellationToken);

            // Остальные кандидаты с K > 0 → дополнительные записи в очереди
            var newMasterId = result.MasterId!.Value;
            foreach (var other in candidateScores.Where(c => c.MasterId != best.MasterId && c.K > 0))
            {
                var review = new PersonReviewQueue
                {
                    Id = Guid.NewGuid(),
                    PersonAId = other.MasterId,
                    PersonBId = newMasterId,
                    SharedKeyType = other.BestMatchedProofKey,
                    ConflictKeyType = string.Join(",", other.ReportConflicts.Select(c => c.KeyType)),
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };
                _context.PersonReviewQueues.Add(review);
            }
            await _context.SaveChangesAsync(cancellationToken);

            return result;
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
            await SaveDefectsAsync(masterIdResult, defects, cancellationToken);
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
            await SaveDefectsAsync(created.MasterId, defects, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var entityDefects = defects.Select(d => new PersonDefect
        {
            Id = Guid.NewGuid(),
            MasterId = masterId,
            DefectType = d.DefectType,
            DefectMessage = d.DefectMessage,
            FieldName = d.FieldName,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _repository.SaveDefectsAsync(entityDefects, cancellationToken);
    }

    private static ExtPerson CreateExtPersonEntity(ResolveRequest request, IReadOnlyList<IdentificationKey> computedKeys)
    {
        return new ExtPerson
        {
            Id = Guid.NewGuid(),
            SourceSystemId = request.SourceSystemId,
            ExternalPersonId = request.ExternalPersonId,
            ExternalPersonType = request.ExternalPersonType,
            KeyInn = computedKeys.FirstOrDefault(k => k.KeyType == "inn")?.KeyValue,
            KeySnils = computedKeys.FirstOrDefault(k => k.KeyType == "snils")?.KeyValue,
            KeyDul = computedKeys.FirstOrDefault(k => k.KeyType == "dul")?.KeyValue,
            KeyInnFio = computedKeys.FirstOrDefault(k => k.KeyType == "inn_fio")?.KeyValue,
            KeySnilsFio = computedKeys.FirstOrDefault(k => k.KeyType == "snils_fio")?.KeyValue,
            KeyDulFio = computedKeys.FirstOrDefault(k => k.KeyType == "dul_fio")?.KeyValue,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static IReadOnlyList<ExtPersonDefect> CreateExtDefectEntities(
        Guid extPersonId,
        IReadOnlyList<DefectInfo> defects)
    {
        return defects.Select(d => new ExtPersonDefect
        {
            Id = Guid.NewGuid(),
            ExtPersonId = extPersonId,
            DefectType = d.DefectType,
            DefectMessage = d.DefectMessage,
            FieldName = d.FieldName,
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
            ExtPersonId = extPersonId,
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
        string matchedKeyType,
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
            await SaveDefectsAsync(newMasterId, defects, cancellationToken);
        }

        await _cessationService.CancelDeferredCessationAsync(
            request.SourceSystemId, request.ExternalPersonId, cancellationToken);

        // Записать в очередь на обработку стюардом
        var review = new PersonReviewQueue
        {
            Id = Guid.NewGuid(),
            PersonAId = existingMasterId,
            PersonBId = newMasterId,
            SharedKeyType = matchedKeyType,
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

    /// <summary>
    /// Вес детерминированного ключа для tie-breaking при равенстве M и K.
    /// ИНН (уникален для ФЛ) > СНИЛС > ДУЛ.
    /// </summary>
    private static int DeterministicKeyWeight(string keyType) => keyType switch
    {
        "inn" => 3,
        "snils" => 2,
        "dul" => 1,
        _ => 0
    };
}
