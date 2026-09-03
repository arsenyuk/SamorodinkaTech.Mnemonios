namespace Mnemonios.Domain.Entities;

/// <summary>
/// Запись отложенной прекращения обработки персональных данных.
/// Отслеживает отложенное удаление, запланированное на будущую дату.
/// </summary>
public class PersonDeferredCessation
{
    /// <summary>Уникальный идентификатор записи.</summary>
    public Guid Id { get; set; }

    /// <summary>Идентификатор персоны, обработка которой будет прекращена.</summary>
    public Guid MasterId { get; set; }

    /// <summary>Идентификатор системы-источника, запрашивающей прекращение.</summary>
    public string SourceSystemId { get; set; } = string.Empty;

    /// <summary>Идентификатор персоны в системе-источнике.</summary>
    public string ExternalPersonId { get; set; } = string.Empty;

    /// <summary>Дата удаления данных персоны.</summary>
    public DateTime ScheduledDeletionDate { get; set; }

    /// <summary>Статус: pending | cancelled | completed.</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Ключ организационной единицы (алиас или ИНН). Stored as-is, no hashing applied.</summary>
    public string OrganizationUnitKey { get; set; } = string.Empty;

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }
}
