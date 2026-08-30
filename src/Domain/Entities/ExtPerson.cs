namespace Mnemonios.Domain.Entities;

/// <summary>
/// Сырые данные запроса идентификации (staging).
/// </summary>
public class ExtPerson
{
    /// <summary>Уникальный идентификатор staging-записи.</summary>
    public Guid Id { get; set; }

    /// <summary>Ссылка на золотую запись (устанавливается после обработки).</summary>
    public Guid? MasterId { get; set; }

    /// <summary>Идентификатор системы-источника.</summary>
    public string SourceSystemId { get; set; } = string.Empty;

    /// <summary>Идентификатор персоны в системе-источнике.</summary>
    public string ExternalPersonId { get; set; } = string.Empty;

    /// <summary>Опциональный типизированный классификатор из системы-источника.</summary>
    public string? ExternalPersonType { get; set; }

    /// <summary>Имя (оригинал, до нормализации).</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Фамилия (оригинал, до нормализации).</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Отчество (оригинал, до нормализации, необязательно).</summary>
    public string? MiddleName { get; set; }

    /// <summary>Сырые данные evidence в JSON (ИНН, СНИЛС, ДУЛ).</summary>
    public string? RawEvidence { get; set; }

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата завершения обработки.</summary>
    public DateTime? ProcessedAt { get; set; }
}
