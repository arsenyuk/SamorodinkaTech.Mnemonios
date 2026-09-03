namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Result of scheduling a deferred cessation of personal data processing.
/// </summary>
public record DeferredCessationResponse
{
    /// <summary>Internal person identifier whose data will be deleted (null if no person found).</summary>
    public Guid? MasterId { get; init; }

    /// <summary>Планируемая дата удаления данных.</summary>
    public DateTime ScheduledDeletionDate { get; init; }
}
