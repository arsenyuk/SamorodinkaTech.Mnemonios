namespace SamorodinkaTech.Mnemonios.Steward.Services;

/// <summary>
/// Сервис бизнес-логики АРМ стюарда.
/// </summary>
public interface IStewardService
{
    /// <summary>
    /// Получить список pending-записей из очереди на обработку.
    /// </summary>
    Task<IReadOnlyList<ReviewQueueItem>> GetPendingReviewsAsync(CancellationToken ct);

    /// <summary>
    /// Подтвердить запись: merge personB → personA.
    /// </summary>
    /// <returns>true, если запись найдена и обработана; false, если запись не найдена.</returns>
    Task<bool> ConfirmReviewAsync(Guid reviewId, CancellationToken ct);

    /// <summary>
    /// Отклонить запись: оставить записи раздельно.
    /// </summary>
    /// <returns>true, если запись найдена и обработана; false, если запись не найдена.</returns>
    Task<bool> RejectReviewAsync(Guid reviewId, CancellationToken ct);
}
