namespace Mnemonios.Domain.Entities;

/// <summary>
/// Запись в очереди на ручную обработку (steward review).
/// Создаётся при Ambiguous — связывает две мастер-записи с расхождением ключей.
/// </summary>
public class PersonReviewQueue
{
    /// <summary>Уникальный идентификатор записи.</summary>
    public Guid Id { get; set; }

    /// <summary>Идентификатор существующей мастер-записи (которая уже была в системе).</summary>
    public Guid PersonAId { get; set; }

    /// <summary>Идентификатор новой мастер-записи (созданной при Ambiguous).</summary>
    public Guid PersonBId { get; set; }

    /// <summary>Тип ключа, по которому совпадение (inn, snils, dul).</summary>
    public string SharedKeyType { get; set; } = string.Empty;

    /// <summary>Тип ключа, по которому расхождение (inn, snils, dul).</summary>
    public string ConflictKeyType { get; set; } = string.Empty;

    /// <summary>Статус: pending | confirmed | rejected.</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата обработки стюардом (null если не обработано).</summary>
    public DateTime? ReviewedAt { get; set; }
}
