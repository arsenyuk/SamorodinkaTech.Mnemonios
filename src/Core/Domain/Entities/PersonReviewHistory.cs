namespace Mnemonios.Domain.Entities;

/// <summary>
/// История разрешённого конфликта.
/// Создаётся автоматически при разрешении конфликта внешней ИС.
/// </summary>
public class PersonReviewHistory
{
    /// <summary>Уникальный идентификатор записи.</summary>
    public Guid Id { get; set; }

    /// <summary>Ссылка на запись в очереди.</summary>
    public Guid ReviewId { get; set; }

    /// <summary>Идентификатор мастер-записи A.</summary>
    public Guid PersonAId { get; set; }

    /// <summary>Идентификатор мастер-записи B.</summary>
    public Guid PersonBId { get; set; }

    /// <summary>Тип ключа совпадения.</summary>
    public string SharedKeyType { get; set; } = string.Empty;

    /// <summary>Тип ключа конфликта.</summary>
    public string ConflictKeyType { get; set; } = string.Empty;

    /// <summary>Результат разрешения: auto_resolved.</summary>
    public string Resolution { get; set; } = string.Empty;

    /// <summary>Кто разобрал: source_system_id внешней ИС.</summary>
    public string ResolvedBy { get; set; } = string.Empty;

    /// <summary>Дата разрешения.</summary>
    public DateTime ResolvedAt { get; set; }

    /// <summary>Детали разрешения (JSON).</summary>
    public string? ResolutionDetails { get; set; }

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }
}
