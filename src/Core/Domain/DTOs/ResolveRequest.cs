namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Request to resolve (identify) a person in the MPI.
/// </summary>
public record ResolveRequest
{
    /// <summary>Фамилия.</summary>
    public required string LastName { get; init; }

    /// <summary>Имя.</summary>
    public required string FirstName { get; init; }

    /// <summary>Отчество (необязательно).</summary>
    public string? MiddleName { get; init; }

    /// <summary>Evidence — доказательства идентичности (ИНН, СНИЛС, ДУЛ).</summary>
    public Evidence? Evidence { get; init; }

    /// <summary>Идентификатор системы-источника.</summary>
    public required string SourceSystemId { get; init; }

    /// <summary>Идентификатор персоны в системе-источнике.</summary>
    public required string ExternalPersonId { get; init; }

    /// <summary>Опциональный типизированный классификатор из системы-источника.</summary>
    public string? ExternalPersonType { get; init; }

    /// <summary>Ключ организационной единицы (алиас или ИНН). Optional, defaults to empty string.</summary>
    public string OrganizationUnitKey { get; init; } = string.Empty;
}
