namespace Mnemonios.Domain.Entities;

/// <summary>
/// HMAC-ключ идентификации для детерминированного сопоставления персон.
/// </summary>
public class PersonIdentificationKey
{
    /// <summary>Уникальный идентификатор ключа.</summary>
    public Guid Id { get; set; }

    /// <summary>Ссылка на персону.</summary>
    public Guid MasterId { get; set; }

    /// <summary>Тип ключа (inn, snils, dul, fio, fio_full, inn_fio, snils_fio, dul_fio).</summary>
    public string KeyType { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 хеш нормализованных данных.</summary>
    public string KeyValue { get; set; } = string.Empty;

    /// <summary>Версия алгоритма нормализации для этого ключа.</summary>
    public int NormalizationVersion { get; set; }

    /// <summary>Ключ организационной единицы (алиас или ИНН). Stored as-is, no hashing applied.</summary>
    public string? OrganizationUnitKey { get; set; }

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }
}
