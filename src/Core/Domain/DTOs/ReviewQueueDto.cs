namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Запись очереди на обработку стюардом.
/// </summary>
public record ReviewQueueDto
{
    /// <summary>Идентификатор записи.</summary>
    public Guid Id { get; init; }

    /// <summary>Идентификатор существующей мастер-записи.</summary>
    public Guid PersonAId { get; init; }

    /// <summary>Идентификатор новой мастер-записи.</summary>
    public Guid PersonBId { get; init; }

    /// <summary>Тип совпавшего ключа.</summary>
    public string SharedKeyType { get; init; } = string.Empty;

    /// <summary>Тип расходящегося ключа.</summary>
    public string ConflictKeyType { get; init; } = string.Empty;

    /// <summary>Статус: pending | confirmed | rejected.</summary>
    public string Status { get; init; } = string.Empty;
}
