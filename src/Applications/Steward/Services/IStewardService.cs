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
    /// Получить детальную информацию о конфликте для просмотра.
    /// </summary>
    /// <returns>Детали конфликта или null, если запись не найдена.</returns>
    Task<ConflictDetailDto?> GetConflictDetailAsync(Guid reviewId, CancellationToken ct);

    /// <summary>
    /// Получить данные мастер-записи для просмотра.
    /// </summary>
    /// <returns>Данные мастер-записи или null, если запись не найдена.</returns>
    Task<PersonData?> GetPersonDataAsync(Guid masterId, CancellationToken ct);

    /// <summary>
    /// Получить список мастер-записей, имеющих дефекты.
    /// </summary>
    Task<IReadOnlyList<PersonDefectsListItem>> GetPersonsWithDefectsAsync(CancellationToken ct);

    /// <summary>
    /// Получить историю разрешённых конфликтов.
    /// </summary>
    Task<IReadOnlyList<ReviewHistoryItem>> GetReviewHistoryAsync(CancellationToken ct);
}
