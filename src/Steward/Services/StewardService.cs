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
}
