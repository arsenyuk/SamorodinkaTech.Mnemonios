namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Запрос идентификации персоны по предвычисленным HMAC-SHA256 хешам.
/// Используется proxy-сервисом: ПДн остаются на стороне источника,
/// в основной сервис передаются только хеши.
/// </summary>
public record HashResolveRequest
{
    /// <summary>Идентификатор системы-источника.</summary>
    public required string SourceSystemId { get; init; }

    /// <summary>Идентификатор персоны в системе-источнике.</summary>
    public required string ExternalPersonId { get; init; }

    /// <summary>Опциональный типизированный классификатор из системы-источника.</summary>
    public string? ExternalPersonType { get; init; }

    /// <summary>Ключ организационной единицы (алиас или ИНН).</summary>
    public string OrganizationUnitKey { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 хеш нормализованного ИНН.</summary>
    public string? KeyInn { get; init; }

    /// <summary>HMAC-SHA256 хеш нормализованного СНИЛС.</summary>
    public string? KeySnils { get; init; }

    /// <summary>HMAC-SHA256 хеш нормализованного ДУЛ.</summary>
    public string? KeyDul { get; init; }

    /// <summary>HMAC-SHA256 хеш составного ключа ИНН+ФИО.</summary>
    public string? KeyInnFio { get; init; }

    /// <summary>HMAC-SHA256 хеш составного ключа СНИЛС+ФИО.</summary>
    public string? KeySnilsFio { get; init; }

    /// <summary>HMAC-SHA256 хеш составного ключа ДУЛ+ФИО.</summary>
    public string? KeyDulFio { get; init; }
}
