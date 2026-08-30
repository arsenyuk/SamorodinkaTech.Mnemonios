namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Response containing the full DUL classifier with flat list and grouped by category.
/// </summary>
public record DulClassifierResponse
{
    /// <summary>All classifier entries.</summary>
    public IReadOnlyList<DulClassifierEntry> Entries { get; init; } = [];

    /// <summary>Entries grouped by category.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<DulClassifierEntry>> ByCategory { get; init; } =
        new Dictionary<string, IReadOnlyList<DulClassifierEntry>>();
}
