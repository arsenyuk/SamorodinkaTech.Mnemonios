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
    private readonly AppDbContext _context;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="PersonResolveService"/>.
    /// </summary>
    public PersonResolveService(
        IPersonRepository repository,
        INormalizationService normalizationService,
        IIdentificationKeyService keyService,
        IPersonCessationService cessationService,
        AppDbContext context)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _normalizationService = normalizationService ?? throw new ArgumentNullException(nameof(normalizationService));
        _keyService = keyService ?? throw new ArgumentNullException(nameof(keyService));
        _cessationService = cessationService ?? throw new ArgumentNullException(nameof(cessationService));
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
        var keyValues = computedKeys.Select(k => k.KeyValue);

        var matchedPersonIds = await _repository.FindPersonIdsByKeysAsync(keyValues, cancellationToken);

        if (matchedPersonIds.Count > 1)
        {
            return new ResolveResponse
            {
                Status = PersonMatchStatus.Conflict,
                MasterId = null,
                HasDefects = defects.Count > 0,
                Defects = defects
            };
        }

        if (matchedPersonIds.Count == 1)
        {
            var masterId = matchedPersonIds[0];

            // Enrich golden record with new data
            await EnrichPersonAsync(masterId, request, cancellationToken);

            await LinkExternalIdAsync(masterId, request, extPerson.Id, cancellationToken);

            await SaveDocumentAsync(masterId, request, computedKeys, cancellationToken);

            if (defects.Count > 0)
            {
                await SaveDefectsAsync(masterId, defects, request, cancellationToken);
            }

            await _cessationService.CancelDeferredCessationAsync(
                request.SourceSystemId, request.ExternalPersonId, cancellationToken);

            var pendingCessationMatched = await _repository.GetPendingDeferredCessationAsync(
                request.SourceSystemId, request.ExternalPersonId, cancellationToken);

            return new ResolveResponse
            {
                Status = PersonMatchStatus.Matched,
                MasterId = masterId,
                HasDefects = defects.Count > 0,
                Defects = defects,
                ScheduledDeletionDate = pendingCessationMatched?.ScheduledDeletionDate
            };
        }

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
}
