namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Entry in the DUL (identity document) classifier.
/// </summary>
public record DulClassifierEntry
{
    /// <summary>Document type code (e.g., "21", "91").</summary>
    public required string Code { get; init; }

    /// <summary>Document type name.</summary>
    public required string Name { get; init; }

    /// <summary>Category: "Граждане РФ" | "Иностранные граждане" | "Иные документы".</summary>
    public required string Category { get; init; }
}
