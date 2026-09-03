namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Внешний системный идентификатор для запроса прекращения.
/// OrganizationUnitKey is optional — used for organization-wide cessation.
/// </summary>
public record CessationIdentifierDto
{
    /// <summary>Идентификатор системы-источника.</summary>
    public required string SourceSystemId { get; init; }

    /// <summary>Идентификатор персоны в системе-источнике.</summary>
    public required string ExternalPersonId { get; init; }
}
