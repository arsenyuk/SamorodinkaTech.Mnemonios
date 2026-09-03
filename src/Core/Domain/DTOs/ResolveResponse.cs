using Mnemonios.Domain.Enums;

namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Response from person resolution.
/// </summary>
public record ResolveResponse
{
    /// <summary>Resolution status.</summary>
    public required PersonMatchStatus Status { get; init; }

    /// <summary>Person ID (null only when Status is Conflict).</summary>
    public Guid? MasterId { get; init; }

    /// <summary>Whether the request had data defects.</summary>
    public bool HasDefects { get; init; }

    /// <summary>List of data defects found during resolution.</summary>
    public IReadOnlyList<DefectInfo> Defects { get; init; } = [];

    /// <summary>Scheduled deletion date if a deferred cessation is pending for this external link.</summary>
    public DateTime? ScheduledDeletionDate { get; init; }

    /// <summary>Список расхождений ключей (при Status = Ambiguous).</summary>
    public IReadOnlyList<KeyConflict> KeyConflicts { get; init; } = [];
}
