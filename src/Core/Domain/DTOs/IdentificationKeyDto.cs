namespace Mnemonios.Domain.DTOs;

/// <summary>
/// DTO для отображения ключа идентификации персоны.
/// Не содержит HMAC-значение ключа (KeyValue) — это секрет.
/// </summary>
public record IdentificationKeyDto
{
    /// <summary>Идентификатор ключа.</summary>
    public Guid Id { get; init; }

    /// <summary>Тип ключа идентификации (inn, snils, dul, fio и т.д.).</summary>
    public required string KeyType { get; init; }

    /// <summary>Версия алгоритма нормализации.</summary>
    public int NormalizationVersion { get; init; }

    /// <summary>Дата создания ключа.</summary>
    public DateTime CreatedAt { get; init; }
}
