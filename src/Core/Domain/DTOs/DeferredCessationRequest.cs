namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Запрос на отложенное прекращение обработки ПДн.
/// Поддерживает конкретные ключи и организационное прекращение.
/// </summary>
public record DeferredCessationRequest
{
    /// <summary>Массив идентификаторов для отложенного удаления.</summary>
    public IReadOnlyList<CessationIdentifierDto> Identifiers { get; init; } = [];

    /// <summary>Дата удаления данных персоны. Must be in the future.</summary>
    public required DateTime ScheduledDeletionDate { get; init; }

    /// <summary>Organization unit key (required if Identifiers is empty).</summary>
    public string? OrganizationUnitKey { get; init; }
}
