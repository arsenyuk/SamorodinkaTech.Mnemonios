namespace Mnemonios.Domain.Entities;

/// <summary>
/// Staging-запись запроса идентификации (без ПДн — только метаданные).
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

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата завершения обработки.</summary>
    public DateTime? ProcessedAt { get; set; }
}
