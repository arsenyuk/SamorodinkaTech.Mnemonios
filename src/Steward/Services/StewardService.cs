using Microsoft.EntityFrameworkCore;
using Mnemonios.Domain.Interfaces;
using Mnemonios.Infrastructure.Persistence;

namespace SamorodinkaTech.Mnemonios.Steward.Services;

/// <summary>
/// Реализация сервиса бизнес-логики АРМ стюарда.
/// </summary>
public class StewardService : IStewardService
{
    private readonly AppDbContext _context;
    private readonly IPersonMergeService _mergeService;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="StewardService"/>.
    /// </summary>
    public StewardService(AppDbContext context, IPersonMergeService mergeService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mergeService = mergeService ?? throw new ArgumentNullException(nameof(mergeService));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReviewQueueItem>> GetPendingReviewsAsync(CancellationToken ct)
    {
        return await _context.PersonReviewQueues
            .Where(r => r.Status == "pending")
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ReviewQueueItem(
                r.Id,
                r.PersonAId,
                r.PersonBId,
                r.SharedKeyType,
                r.ConflictKeyType,
                r.CreatedAt))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmReviewAsync(Guid reviewId, CancellationToken ct)
    {
        var review = await _context.PersonReviewQueues.FindAsync([reviewId], ct);
        if (review is null)
            return false;

        await _mergeService.MergePersonsAsync(review.PersonAId, review.PersonBId, "steward_confirm", ct);

        review.Status = "confirmed";
        review.ReviewedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RejectReviewAsync(Guid reviewId, CancellationToken ct)
    {
        var review = await _context.PersonReviewQueues.FindAsync([reviewId], ct);
        if (review is null)
            return false;

        review.Status = "rejected";
        review.ReviewedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return true;
    }

    /// <inheritdoc/>
    public async Task<ConflictDetailDto?> GetConflictDetailAsync(Guid reviewId, CancellationToken ct)
    {
        var review = await _context.PersonReviewQueues.FindAsync([reviewId], ct);
        if (review is null)
            return null;

        var personA = await LoadPersonDataAsync(review.PersonAId, ct);
        var personB = await LoadPersonDataAsync(review.PersonBId, ct);

        var comparisons = BuildKeyComparisons(personA, personB);

        return new ConflictDetailDto
        {
            Review = new ReviewQueueItem(
                review.Id,
                review.PersonAId,
                review.PersonBId,
                review.SharedKeyType,
                review.ConflictKeyType,
                review.CreatedAt),
            PersonA = personA,
            PersonB = personB,
            KeyComparisons = comparisons
        };
    }

    /// <inheritdoc/>
    public async Task<PersonData?> GetPersonDataAsync(Guid masterId, CancellationToken ct)
    {
        var person = await _context.Persons.FindAsync([masterId], ct);
        if (person is null)
            return null;

        return await LoadPersonDataAsync(masterId, ct);
    }

    private async Task<PersonData> LoadPersonDataAsync(Guid masterId, CancellationToken ct)
    {
        var person = await _context.Persons.FindAsync([masterId], ct);
        var externalIds = await _context.PersonExternalIds
            .Where(e => e.MasterId == masterId)
            .ToListAsync(ct);
        var keys = await _context.PersonIdentificationKeys
            .Where(k => k.MasterId == masterId)
            .ToListAsync(ct);
        var defects = await _context.PersonDefects
            .Where(d => d.MasterId == masterId)
            .ToListAsync(ct);
        var documents = await _context.PersonDocuments
            .Where(d => d.MasterId == masterId)
            .ToListAsync(ct);

        return new PersonData
        {
            MasterId = masterId,
            CreatedAt = person?.CreatedAt ?? DateTime.MinValue,
            UpdatedAt = person?.UpdatedAt ?? DateTime.MinValue,
            ExternalIds = externalIds.Select(e => new ExternalIdInfo
            {
                SourceSystemId = e.SourceSystemId,
                ExternalPersonId = e.ExternalPersonId,
                ExternalPersonType = e.ExternalPersonType,
                OrganizationUnitKey = e.OrganizationUnitKey
            }).ToList(),
            IdentificationKeys = keys.Select(k => new KeyInfo
            {
                KeyType = k.KeyType,
                KeyValuePreview = k.KeyValue.Length > 16 ? k.KeyValue[..16] : k.KeyValue
            }).ToList(),
            Defects = defects.Select(d => new DefectInfo
            {
                DefectType = d.DefectType,
                DefectMessage = d.DefectMessage,
                FieldName = d.FieldName
            }).ToList(),
            Documents = documents.Select(d => new DocumentInfo
            {
                DocumentType = d.DocumentType,
                DocumentHashPreview = d.DocumentHash.Length > 16 ? d.DocumentHash[..16] : d.DocumentHash
            }).ToList()
        };
    }

    private static IReadOnlyList<KeyComparison> BuildKeyComparisons(PersonData personA, PersonData personB)
    {
        var allKeyTypes = new[] { "inn", "snils", "dul", "inn_fio", "snils_fio", "dul_fio" };
        var keysA = personA.IdentificationKeys.ToDictionary(k => k.KeyType, k => k.KeyValuePreview);
        var keysB = personB.IdentificationKeys.ToDictionary(k => k.KeyType, k => k.KeyValuePreview);

        var result = new List<KeyComparison>();
        foreach (var keyType in allKeyTypes)
        {
            keysA.TryGetValue(keyType, out var valueA);
            keysB.TryGetValue(keyType, out var valueB);

            string status;
            if (valueA is not null && valueB is not null)
                status = valueA == valueB ? "match" : "conflict";
            else if (valueA is not null)
                status = "only_a";
            else
                status = "only_b";

            result.Add(new KeyComparison
            {
                KeyType = keyType,
                KeyValueA = valueA,
                KeyValueB = valueB,
                Status = status
            });
        }

        return result;
    }
}
