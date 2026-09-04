using Microsoft.EntityFrameworkCore;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Entities;
using Mnemonios.Domain.Enums;
using Mnemonios.Domain.Interfaces;
using Mnemonios.Infrastructure.Persistence;

namespace Mnemonios.Infrastructure.Services;

/// <summary>
/// Сервис идентификации персон по предвычисленным HMAC-SHA256 хешам.
/// Proxy-сервис вычисляет хеши на стороне источника ПДн и передаёт
/// только хеши в основной сервис для матчинга.
/// </summary>
public class PersonHashResolveService : IPersonHashResolveService
{
    private const int DefaultNormalizationVersion = 1;

    private readonly IPersonRepository _repository;
    private readonly IPersonCessationService _cessationService;
    private readonly IClientIpProvider _clientIpProvider;
    private readonly AppDbContext _context;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="PersonHashResolveService"/>.
    /// </summary>
    public PersonHashResolveService(
        IPersonRepository repository,
        IPersonCessationService cessationService,
        IClientIpProvider clientIpProvider,
        AppDbContext context)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cessationService = cessationService ?? throw new ArgumentNullException(nameof(cessationService));
        _clientIpProvider = clientIpProvider ?? throw new ArgumentNullException(nameof(clientIpProvider));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task<ResolveResponse> ResolveByHashesAsync(
        HashResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        var computedKeys = BuildKeysFromRequest(request);
        if (computedKeys.Count == 0)
            throw new ArgumentException("Необходимо передать хотя бы один HMAC-SHA256 хеш (KeyInn, KeySnils, KeyDul или составной ключ).");

        var extPerson = CreateExtPersonEntity(request, computedKeys, _clientIpProvider.GetClientIp());

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.ExtPersons.Add(extPerson);
            await _context.SaveChangesAsync(cancellationToken);

            var response = await ResolveByMatchingAsync(
                request, extPerson, computedKeys, cancellationToken);

            var goldenMasterId = response.MasterId;
            await _repository.MarkExtPersonProcessedAsync(extPerson.Id, goldenMasterId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            if (response.Status == PersonMatchStatus.Matched && response.MasterId.HasValue)
            {
                await ClosePendingReviewAsync(request.SourceSystemId, request.ExternalPersonId, response.MasterId.Value, cancellationToken);
            }

            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<ResolveResponse> ResolveByMatchingAsync(
        HashResolveRequest request,
        ExtPerson extPerson,
        IReadOnlyList<IdentificationKey> computedKeys,
        CancellationToken cancellationToken)
    {
        var existingExtId = await _context.PersonExternalIds
            .FirstOrDefaultAsync(e =>
                e.SourceSystemId == request.SourceSystemId &&
                e.ExternalPersonId == request.ExternalPersonId, cancellationToken);

        if (existingExtId is not null)
        {
            var matchedMasterId = existingExtId.MasterId;

            await UpdatePersonTimestampAsync(matchedMasterId, cancellationToken);
            await SaveNewKeysAsync(matchedMasterId, computedKeys, cancellationToken);
            await LinkExternalIdAsync(matchedMasterId, request, extPerson.Id, cancellationToken);

            await _cessationService.CancelDeferredCessationAsync(
                request.SourceSystemId, request.ExternalPersonId, cancellationToken);

            var pendingCessation = await _repository.GetPendingDeferredCessationAsync(
                request.SourceSystemId, request.ExternalPersonId, cancellationToken);

            return new ResolveResponse
            {
                Status = PersonMatchStatus.Matched,
                MasterId = matchedMasterId,
                ScheduledDeletionDate = pendingCessation?.ScheduledDeletionDate
            };
        }

        var allMatchingKeys = computedKeys
            .Where(k => k.KeyType is "inn" or "snils" or "dul" or "inn_fio" or "snils_fio" or "dul_fio")
            .ToList();
        var matchingKeyValues = allMatchingKeys.Select(k => k.KeyValue);

        var candidateIds = await _repository.FindPersonIdsByKeysAsync(matchingKeyValues, cancellationToken);

        if (candidateIds.Count == 0)
        {
            return await CreateNewPersonAsync(request, extPerson, computedKeys, cancellationToken);
        }

        var candidateScores = new List<(Guid MasterId, int M, int K, int D, List<KeyConflict> ScoringConflicts, List<KeyConflict> ReportConflicts, string BestMatchedProofKey)>();

        foreach (var masterId in candidateIds)
        {
            var existingKeys = await _context.PersonIdentificationKeys
                .Where(k => k.MasterId == masterId)
                .ToListAsync(cancellationToken);

            int m = 0, k = 0, d = 0;
            var scoringConflicts = new List<KeyConflict>();
            var reportConflicts = new List<KeyConflict>();

            var proofKeys = allMatchingKeys.Where(k => k.KeyType is "inn" or "snils" or "dul").ToList();
            var compositeKeys = allMatchingKeys.Where(k => k.KeyType is "inn_fio" or "snils_fio" or "dul_fio").ToList();

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

            foreach (var requestKey in compositeKeys)
            {
                var existing = existingKeys.FirstOrDefault(e => e.KeyType == requestKey.KeyType);
                if (existing is null) continue;

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
                        k++;
                        scoringConflicts.Add(new KeyConflict(requestKey.KeyType));
                    }
                }
            }

            var bestMatched = matchedProofTypes
                .OrderByDescending(t => DeterministicKeyWeight(t))
                .FirstOrDefault() ?? string.Empty;

            candidateScores.Add((masterId, m, k, d, scoringConflicts, reportConflicts, bestMatched));
        }

        var best = candidateScores
            .OrderBy(c => c.K)
            .ThenByDescending(c => c.D)
            .ThenByDescending(c => c.M)
            .First();

        if (best.K > 0)
        {
            var result = await HandleAmbiguousAsync(
                best.MasterId, best.BestMatchedProofKey, best.ReportConflicts,
                request, extPerson, computedKeys, cancellationToken);

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
            return await CreateNewPersonAsync(request, extPerson, computedKeys, cancellationToken);
        }

        var masterIdResult = best.MasterId;

        await UpdatePersonTimestampAsync(masterIdResult, cancellationToken);
        await SaveNewKeysAsync(masterIdResult, computedKeys, cancellationToken);
        await LinkExternalIdAsync(masterIdResult, request, extPerson.Id, cancellationToken);

        await _cessationService.CancelDeferredCessationAsync(
            request.SourceSystemId, request.ExternalPersonId, cancellationToken);

        var pendingCessationMatched = await _repository.GetPendingDeferredCessationAsync(
            request.SourceSystemId, request.ExternalPersonId, cancellationToken);

        return new ResolveResponse
        {
            Status = PersonMatchStatus.Matched,
            MasterId = masterIdResult,
            ScheduledDeletionDate = pendingCessationMatched?.ScheduledDeletionDate
        };
    }

    private async Task<ResolveResponse> CreateNewPersonAsync(
        HashResolveRequest request,
        ExtPerson extPerson,
        IReadOnlyList<IdentificationKey> computedKeys,
        CancellationToken cancellationToken)
    {
        var newMasterId = Guid.NewGuid();
        var person = new Person
        {
            MasterId = newMasterId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var identificationKeys = CreateKeyEntities(newMasterId, computedKeys, request.OrganizationUnitKey);
        var externalId = CreateExternalIdEntity(newMasterId, request, extPerson.Id);

        var created = await _repository.CreateAsync(person, identificationKeys, externalId, cancellationToken);

        await _cessationService.CancelDeferredCessationAsync(
            request.SourceSystemId, request.ExternalPersonId, cancellationToken);

        return new ResolveResponse
        {
            Status = PersonMatchStatus.Unmatched,
            MasterId = created.MasterId
        };
    }

    private async Task<ResolveResponse> HandleAmbiguousAsync(
        Guid existingMasterId,
        string matchedKeyType,
        List<KeyConflict> conflicts,
        HashResolveRequest request,
        ExtPerson extPerson,
        IReadOnlyList<IdentificationKey> computedKeys,
        CancellationToken cancellationToken)
    {
        var newMasterId = Guid.NewGuid();
        var person = new Person
        {
            MasterId = newMasterId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var identificationKeys = CreateKeyEntities(newMasterId, computedKeys, request.OrganizationUnitKey);

        var existingExtId = await _context.PersonExternalIds
            .FirstOrDefaultAsync(e =>
                e.SourceSystemId == request.SourceSystemId &&
                e.ExternalPersonId == request.ExternalPersonId, cancellationToken);

        if (existingExtId is not null)
        {
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

        await _cessationService.CancelDeferredCessationAsync(
            request.SourceSystemId, request.ExternalPersonId, cancellationToken);

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
            KeyConflicts = conflicts
        };
    }

    private static IReadOnlyList<IdentificationKey> BuildKeysFromRequest(HashResolveRequest request)
    {
        var keys = new List<IdentificationKey>();

        if (!string.IsNullOrWhiteSpace(request.KeyInn))
            keys.Add(new IdentificationKey { KeyType = "inn", KeyValue = request.KeyInn });

        if (!string.IsNullOrWhiteSpace(request.KeySnils))
            keys.Add(new IdentificationKey { KeyType = "snils", KeyValue = request.KeySnils });

        if (!string.IsNullOrWhiteSpace(request.KeyDul))
            keys.Add(new IdentificationKey { KeyType = "dul", KeyValue = request.KeyDul });

        if (!string.IsNullOrWhiteSpace(request.KeyInnFio))
            keys.Add(new IdentificationKey { KeyType = "inn_fio", KeyValue = request.KeyInnFio });

        if (!string.IsNullOrWhiteSpace(request.KeySnilsFio))
            keys.Add(new IdentificationKey { KeyType = "snils_fio", KeyValue = request.KeySnilsFio });

        if (!string.IsNullOrWhiteSpace(request.KeyDulFio))
            keys.Add(new IdentificationKey { KeyType = "dul_fio", KeyValue = request.KeyDulFio });

        return keys;
    }

    private static ExtPerson CreateExtPersonEntity(HashResolveRequest request, IReadOnlyList<IdentificationKey> computedKeys, string sourceIp)
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
            SourceIp = sourceIp,
            CreatedAt = DateTime.UtcNow
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

    private static PersonExternalId CreateExternalIdEntity(Guid masterId, HashResolveRequest request, Guid extPersonId)
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

    private async Task LinkExternalIdAsync(
        Guid masterId,
        HashResolveRequest request,
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

    private async Task UpdatePersonTimestampAsync(Guid masterId, CancellationToken cancellationToken)
    {
        var person = await _repository.GetByIdAsync(masterId, cancellationToken);
        if (person is null)
            return;

        person.UpdatedAt = DateTime.UtcNow;
        _context.Persons.Update(person);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveNewKeysAsync(
        Guid masterId,
        IReadOnlyList<IdentificationKey> computedKeys,
        CancellationToken cancellationToken)
    {
        var existingKeyValues = await _context.PersonIdentificationKeys
            .Where(k => k.MasterId == masterId)
            .Select(k => k.KeyValue)
            .ToListAsync(cancellationToken);

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

    private async Task ClosePendingReviewAsync(
        string sourceSystemId,
        string externalPersonId,
        Guid resolvedMasterId,
        CancellationToken cancellationToken)
    {
        var pendingReview = await _context.PersonReviewQueues
            .FirstOrDefaultAsync(r =>
                r.Status == "pending" &&
                _context.PersonExternalIds.Any(e =>
                    e.MasterId == r.PersonAId &&
                    e.SourceSystemId == sourceSystemId &&
                    e.ExternalPersonId == externalPersonId),
                cancellationToken);

        if (pendingReview is null)
            return;

        var history = new PersonReviewHistory
        {
            Id = Guid.NewGuid(),
            ReviewId = pendingReview.Id,
            PersonAId = pendingReview.PersonAId,
            PersonBId = pendingReview.PersonBId,
            SharedKeyType = pendingReview.SharedKeyType,
            ConflictKeyType = pendingReview.ConflictKeyType,
            Resolution = "auto_resolved",
            ResolvedBy = sourceSystemId,
            ResolvedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _context.PersonReviewHistories.Add(history);

        _context.PersonReviewQueues.Remove(pendingReview);
        await _context.SaveChangesAsync(cancellationToken);
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
