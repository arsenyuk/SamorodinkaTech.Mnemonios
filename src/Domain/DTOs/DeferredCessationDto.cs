namespace Mnemonios.Domain.DTOs;

/// <summary>
/// DTO для отображения записи отложенной прекращения обработки персональных данных.
/// </summary>
public record DeferredCessationDto
{
    /// <summary>Идентификатор записи.</summary>
    public Guid Id { get; init; }

    /// <summary>Идентификатор системы-источника.</summary>
    public required string SourceSystemId { get; init; }

    /// <summary>Внешний идентификатор персоны в системе-источнике.</summary>
    public required string ExternalPersonId { get; init; }

    /// <summary>Планируемая дата удаления данных.</summary>
    public DateTime ScheduledDeletionDate { get; init; }

    /// <summary>Статус записи (pending, completed, cancelled).</summary>
    public required string Status { get; init; }

    /// <summary>Ключ организационной единицы (опционально).</summary>
    public string? OrganizationUnitKey { get; init; }

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; init; }
}
