using Mnemonios.Domain.DTOs;

namespace Mnemonios.Domain.Interfaces;

/// <summary>
/// Сервис прекращения обработки персональных данных во всех системах.
/// Поддерживает мгновенное и отложенное прекращение.
/// </summary>
public interface IPersonCessationService
{
    /// <summary>
    /// Ceases processing personal data immediately for a person identified by their external system link.
    /// Deletes all identification keys, defects, external ID links, and the person record.
    /// Returns null if no matching person is found.
    /// </summary>
    Task<CessationResponse?> CeaseProcessingAsync(
        CessationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules deferred cessation of personal data processing.
    /// Data will be deleted on the specified future date.
    /// Returns null if no matching person is found.
    /// </summary>
    Task<DeferredCessationResponse?> DeferProcessingAsync(
        DeferredCessationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a pending deferred cessation for the specified external system link.
    /// Called when the same person data is added again via resolve.
    /// </summary>
    Task CancelDeferredCessationAsync(
        string sourceSystemId,
        string externalPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Реконсилизация: удаление помеченных staging-записей и золотых записей
    /// для лиц, у которых не осталось внешних ссылок.
    /// Возвращает количество обработанных записей.
    /// </summary>
    Task<int> ReconcileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Обработка отложенных отзывов, дата которых наступила.
    /// Преобразует person_deferred_cessations (status='pending', date &lt;= NOW())
    /// в ext_person_cessations (processing_status='cessation') для удаления через ReconcileAsync.
    /// Возвращает количество обработанных записей.
    /// </summary>
    Task<int> ProcessDeferredCessationsAsync(CancellationToken cancellationToken = default);
}
