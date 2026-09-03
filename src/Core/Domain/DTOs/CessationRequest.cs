namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Запрос на прекращение обработки ПДн.
/// Поддерживает конкретные ключи и организационное прекращение.
/// </summary>
public record CessationRequest
{
    /// <summary>Массив идентификаторов для удаления. If empty — deletes all links by OrganizationUnitKey.</summary>
    public IReadOnlyList<CessationIdentifierDto> Identifiers { get; init; } = [];

    /// <summary>Organization unit key (required if Identifiers is empty — deletes all links for this organization).</summary>
    public string? OrganizationUnitKey { get; init; }
}
