namespace Mnemonios.Domain.Interfaces;

/// <summary>
/// Сервис автоматического слияния двух мастер-записей при обнаружении конфликта.
/// Переносит ключи, внешние ссылки и документы из merged в surviving,
/// удаляет merged-запись.
/// </summary>
public interface IPersonMergeService
{
    /// <summary>
    /// Выполнить слияние: перенести данные из merged в surviving, удалить merged.
    /// </summary>
    /// <param name="survivingMasterId">Идентификатор выжившей записи.</param>
    /// <param name="mergedMasterId">Идентификатор поглощаемой записи.</param>
    /// <param name="reason">Причина слияния (для логирования).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task MergePersonsAsync(
        Guid survivingMasterId,
        Guid mergedMasterId,
        string reason,
        CancellationToken cancellationToken = default);
}
