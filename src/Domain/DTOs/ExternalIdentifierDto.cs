namespace Mnemonios.Domain.DTOs;

/// <summary>
/// DTO внешнего системного идентификатора.
/// </summary>
public record ExternalIdentifierDto
{
    /// <summary>Идентификатор системы-источника.</summary>
    public required string SourceSystemId { get; init; }

    /// <summary>Идентификатор персоны в системе-источнике.</summary>
    public required string ExternalPersonId { get; init; }

    /// <summary>Опциональный типизированный классификатор из системы-источника.</summary>
    public string? ExternalPersonType { get; init; }
}
