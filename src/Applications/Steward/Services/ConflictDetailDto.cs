namespace SamorodinkaTech.Mnemonios.Steward.Services;

/// <summary>
/// DTO для отображения деталей конфликта между двумя персонами.
/// </summary>
public record ConflictDetailDto
{
    /// <summary>Информация из очереди на обработку.</summary>
    public required ReviewQueueItem Review { get; init; }

    /// <summary>Данные персоны A (существующая мастер-запись).</summary>
    public required PersonData PersonA { get; init; }

    /// <summary>Данные персоны B (новая мастер-запись, созданная при Ambiguous).</summary>
    public required PersonData PersonB { get; init; }

    /// <summary>Сравнение ключей идентификации.</summary>
    public required IReadOnlyList<KeyComparison> KeyComparisons { get; init; }
}

/// <summary>
/// Данные одной персоны для отображения (без ПДн).
/// </summary>
public record PersonData
{
    /// <summary>Мастер-идентификатор.</summary>
    public Guid MasterId { get; init; }

    /// <summary>Дата создания.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Дата обновления.</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Внешние ссылки (системы-источники).</summary>
    public IReadOnlyList<ExternalIdInfo> ExternalIds { get; init; } = [];

    /// <summary>Ключи идентификации (хеши).</summary>
    public IReadOnlyList<KeyInfo> IdentificationKeys { get; init; } = [];

    /// <summary>Дефекты данных.</summary>
    public IReadOnlyList<DefectInfo> Defects { get; init; } = [];

    /// <summary>Документы ДУЛ.</summary>
    public IReadOnlyList<DocumentInfo> Documents { get; init; } = [];

    /// <summary>Staging-записи (сырые запросы из ИС).</summary>
    public IReadOnlyList<ExtPersonInfo> ExtPersons { get; init; } = [];
}

/// <summary>
/// Информация о внешней ссылке.
/// </summary>
public record ExternalIdInfo
{
    /// <summary>Идентификатор системы-источника.</summary>
    public string SourceSystemId { get; init; } = string.Empty;

    /// <summary>Идентификатор персоны во внешней системе.</summary>
    public string ExternalPersonId { get; init; } = string.Empty;

    /// <summary>Типизированный классификатор (опционально).</summary>
    public string? ExternalPersonType { get; init; }

    /// <summary>Ключ юридического лица (организационная единица).</summary>
    public string? OrganizationUnitKey { get; init; }
}

/// <summary>
/// Информация о ключе идентификации.
/// </summary>
public record KeyInfo
{
    /// <summary>Тип ключа (inn, snils, dul, inn_fio, snils_fio, dul_fio).</summary>
    public string KeyType { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 хеш (первые 16 символов для отображения).</summary>
    public string KeyValuePreview { get; init; } = string.Empty;
}

/// <summary>
/// Информация о дефекте данных.
/// </summary>
public record DefectInfo
{
    /// <summary>Тип дефекта.</summary>
    public string DefectType { get; init; } = string.Empty;

    /// <summary>Описание дефекта.</summary>
    public string DefectMessage { get; init; } = string.Empty;

    /// <summary>Имя поля.</summary>
    public string? FieldName { get; init; }
}

/// <summary>
/// Информация о документе ДУЛ.
/// </summary>
public record DocumentInfo
{
    /// <summary>Тип документа.</summary>
    public string DocumentType { get; init; } = string.Empty;

    /// <summary>Хеш документа (первые 16 символов).</summary>
    public string DocumentHashPreview { get; init; } = string.Empty;
}

/// <summary>
/// Информация о staging-запросе (ext_persons, без ПДн).
/// </summary>
public record ExtPersonInfo
{
    /// <summary>Идентификатор staging-записи.</summary>
    public Guid Id { get; init; }

    /// <summary>Идентификатор системы-источника.</summary>
    public string SourceSystemId { get; init; } = string.Empty;

    /// <summary>Идентификатор персоны во внешней системе.</summary>
    public string ExternalPersonId { get; init; } = string.Empty;

    /// <summary>Типизированный классификатор.</summary>
    public string? ExternalPersonType { get; init; }

    /// <summary>Дата создания запроса.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Дефекты этого staging-запроса.</summary>
    public IReadOnlyList<ExtPersonDefectInfo> Defects { get; init; } = [];
}

/// <summary>
/// Информация о дефекте staging-запроса.
/// </summary>
public record ExtPersonDefectInfo
{
    /// <summary>Тип дефекта.</summary>
    public string DefectType { get; init; } = string.Empty;

    /// <summary>Описание дефекта.</summary>
    public string DefectMessage { get; init; } = string.Empty;

    /// <summary>Имя поля.</summary>
    public string? FieldName { get; init; }
}

/// <summary>
/// Сравнение одного ключа между двумя персонами.
/// </summary>
public record KeyComparison
{
    /// <summary>Тип ключа (inn, snils, dul, inn_fio, snils_fio, dul_fio).</summary>
    public string KeyType { get; init; } = string.Empty;

    /// <summary>Хеш ключа персоны A (null если ключ отсутствует).</summary>
    public string? KeyValueA { get; init; }

    /// <summary>Хеш ключа персоны B (null если ключ отсутствует).</summary>
    public string? KeyValueB { get; init; }

    /// <summary>Статус сравнения: match | conflict | only_a | only_b.</summary>
    public string Status { get; init; } = string.Empty;
}
