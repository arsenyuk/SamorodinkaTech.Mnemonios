namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Evidence — доказательства идентичности физического лица.
/// ДУЛ (тип, серия, номер), СНИЛС, ИНН. ФИО не являются доказательствами.
/// </summary>
public record Evidence
{
    /// <summary>Tax identification number (optional).</summary>
    public string? Inn { get; init; }

    /// <summary>Individual insurance number (optional).</summary>
    public string? Snils { get; init; }

    /// <summary>Identity document type (DUL type code, optional).</summary>
    public string? DulType { get; init; }

    /// <summary>Document name for code 91 (required when DulType=91).</summary>
    public string? DulTypeName { get; init; }

    /// <summary>Identity document series (optional).</summary>
    public string? DulSeries { get; init; }

    /// <summary>Identity document number (optional).</summary>
    public string? DulNumber { get; init; }
}
