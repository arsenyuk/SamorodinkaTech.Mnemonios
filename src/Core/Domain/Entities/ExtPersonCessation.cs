namespace Mnemonios.Domain.Entities;

/// <summary>
/// Сырые данные запроса прекращения (staging).
/// </summary>
public class ExtPersonCessation
{
    /// <summary>Уникальный идентификатор staging-записи.</summary>
    public Guid Id { get; set; }

    /// <summary>Link to the golden person record (set after processing).</summary>
    public Guid PersonId { get; set; }
    
    /// <summary>Идентификатор системы-источника.</summary>
    public string SourceSystemId { get; set; } = string.Empty;

    /// <summary>Идентификатор персоны в системе-источнике.</summary>
    public string ExternalPersonId { get; set; } = string.Empty;

    /// <summary>Ключ организационной единицы.</summary>
    public string OrganizationUnitKey { get; set; } = string.Empty;

    /// <summary>Статус обработки (pending, cessation).</summary>
    public string ProcessingStatus { get; set; } = "pending";

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата завершения обработки.</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>IP-адрес источника вызова.</summary>
    public string? SourceIp { get; set; }
}
