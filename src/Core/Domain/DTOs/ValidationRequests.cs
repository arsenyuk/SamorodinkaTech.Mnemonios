namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Request to validate an INN.
/// </summary>
public record InnValidationRequest
{
    /// <summary>INN value to validate (10 or 12 digits).</summary>
    public required string Inn { get; init; }
}

/// <summary>
/// Request to validate a SNILS.
/// </summary>
public record SnilsValidationRequest
{
    /// <summary>SNILS value to validate (11 digits).</summary>
    public required string Snils { get; init; }
}

/// <summary>
/// Validation result for INN or SNILS.
/// </summary>
public record ValidationResultDto
{
    /// <summary>Whether the value is valid.</summary>
    public bool IsValid { get; init; }

    /// <summary>Error message if invalid (null if valid).</summary>
    public string? Error { get; init; }
}
