using Mnemonios.Domain.Entities;

namespace Mnemonios.Domain.Interfaces;

/// <summary>
/// Репозиторий для сохранения и получения данных персон.
/// </summary>
public interface IPersonRepository
{
    /// <summary>
    /// Находит персону по совпадению любого из предоставленных значений ключей идентификации.
    /// Возвращает уникальные идентификаторы персон, найденные по всем ключам.
    /// </summary>
    Task<IReadOnlyList<Guid>> FindPersonIdsByKeysAsync(
        IEnumerable<string> keyValues,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает персону по идентификатору.
    /// </summary>
    Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает внешние идентификаторы персоны, опционально фильтруя по ID систем.
    /// </summary>
    Task<IReadOnlyList<PersonExternalId>> GetExternalIdsAsync(
        Guid masterId,
        IEnumerable<string>? sourceSystemIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Атомарно создаёт новую персону с ключами идентификации и внешней ссылкой.
    /// </summary>
    Task<Person> CreateAsync(
        Person person,
        IEnumerable<PersonIdentificationKey> keys,
        PersonExternalId externalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет новую внешнюю ссылку к существующей персоне.
    /// </summary>
    Task AddExternalIdAsync(
        PersonExternalId externalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет внешний идентификатор, если он уже существует для той же системы.
    /// Возвращает (true, existingId) если запись обновлена, (false, null) если нужно вставить новую.
    /// </summary>
    Task<(bool Updated, Guid? ExistingId)> TryUpdateExternalIdAsync(
        PersonExternalId externalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет записи дефектов для персоны.
    /// </summary>
    Task SaveDefectsAsync(
        IEnumerable<PersonDefect> defects,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает ключи идентификации для персоны.
    /// </summary>
    Task<IReadOnlyList<PersonIdentificationKey>> GetIdentificationKeysAsync(
        Guid masterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает записи дефектов для персоны.
    /// </summary>
    Task<IReadOnlyList<PersonDefect>> GetDefectsAsync(
        Guid masterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает записи отложенного прекращения для персоны.
    /// </summary>
    Task<IReadOnlyList<PersonDeferredCessation>> GetDeferredCessationsAsync(
        Guid masterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Находит все ключи идентификации по ключу организационной единицы.
    /// </summary>
    Task<IReadOnlyList<PersonIdentificationKey>> GetIdentificationKeysByOrganizationUnitKeyAsync(
        string organizationUnitKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Находит идентификатор персоны по внешней ссылке.
    /// Возвращает null, если запись не найдена.
    /// </summary>
    Task<Guid?> FindMasterIdByExternalIdAsync(
        string sourceSystemId,
        string externalPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Атомарно удаляет персону и все связанные данные в одной транзакции.
    /// </summary>
    Task DeletePersonDataAsync(
        Person person,
        IEnumerable<PersonIdentificationKey> keys,
        IEnumerable<PersonExternalId> externalIds,
        IEnumerable<PersonDefect> defects,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет конкретную внешнюю ссылку по системе и внешнему ID.
    /// </summary>
    Task DeleteExternalIdAsync(
        string sourceSystemId,
        string externalPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает запись отложенного прекращения для указанной внешней ссылки.
    /// Возвращает null, если записи нет.
    /// </summary>
    Task<PersonDeferredCessation?> GetPendingDeferredCessationAsync(
        string sourceSystemId,
        string externalPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет новую запись отложенного прекращения.
    /// </summary>
    Task AddDeferredCessationAsync(
        PersonDeferredCessation cessation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отменяет отложенное прекращение, устанавливая статус 'cancelled'.
    /// </summary>
    Task CancelDeferredCessationRecordAsync(
        PersonDeferredCessation cessation,
        CancellationToken cancellationToken = default);

    // =========================================================================
    // Staging (ext_*) методы
    // =========================================================================

    /// <summary>
    /// Создаёт staging-запись из сырых входящих данных.
    /// </summary>
    Task<ExtPerson> CreateExtPersonAsync(
        ExtPerson extPerson,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет staging-записи дефектов, связанные с ext_persons.
    /// </summary>
    Task SaveExtDefectsAsync(
        IEnumerable<ExtPersonDefect> defects,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт staging-запись прекращения из сырых данных.
    /// </summary>
    Task<ExtPersonCessation> CreateExtCessationAsync(
        ExtPersonCessation cessation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт staging-запись отложенного прекращения из сырых данных.
    /// </summary>
    Task<ExtPersonDeferredCessation> CreateExtDeferredCessationAsync(
        ExtPersonDeferredCessation cessation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отмечает staging-запись как обработанную и связывает с золотой записью.
    /// </summary>
    Task MarkExtPersonProcessedAsync(
        Guid extPersonId,
        Guid? masterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отмечает staging-запись прекращения как обработанную.
    /// </summary>
    Task MarkExtCessationProcessedAsync(
        Guid extCessationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отмечает staging-запись отложенного прекращения как обработанную.
    /// </summary>
    Task MarkExtDeferredCessationProcessedAsync(
        Guid extDeferredCessationId,
        CancellationToken cancellationToken = default);
}
