namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Request to add a new external identifier link to a person.
/// </summary>
public record AddExternalIdRequest
{
    /// <summary>Идентификатор системы-источника.</summary>
    public required string SourceSystemId { get; init; }

    /// <summary>Идентификатор персоны в системе-источнике.</summary>
    public required string ExternalPersonId { get; init; }

    /// <summary>Опциональный типизированный классификатор из системы-источника.</summary>
    public string? ExternalPersonType { get; init; }
}
