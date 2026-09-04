namespace Mnemonios.Domain.Entities;

/// <summary>
/// Staging-запись запроса идентификации (без ПДн — только метаданные).
/// </summary>
public class ExtPerson
{
    /// <summary>Уникальный идентификатор staging-записи.</summary>
    public Guid Id { get; set; }

    /// <summary>Ссылка на золотую запись (устанавливается после обработки).</summary>
    public Guid? MasterId { get; set; }

    /// <summary>Идентификатор системы-источника.</summary>
    public string SourceSystemId { get; set; } = string.Empty;

    /// <summary>Идентификатор персоны в системе-источнике.</summary>
    public string ExternalPersonId { get; set; } = string.Empty;

    /// <summary>Опциональный типизированный классификатор из системы-источника.</summary>
    public string? ExternalPersonType { get; set; }

    /// <summary>HMAC-хеш ключа inn.</summary>
    public string? KeyInn { get; set; }

    /// <summary>HMAC-хеш ключа snils.</summary>
    public string? KeySnils { get; set; }

    /// <summary>HMAC-хеш ключа dul.</summary>
    public string? KeyDul { get; set; }

    /// <summary>HMAC-хеш ключа inn_fio.</summary>
    public string? KeyInnFio { get; set; }

    /// <summary>HMAC-хеш ключа snils_fio.</summary>
    public string? KeySnilsFio { get; set; }

    /// <summary>HMAC-хеш ключа dul_fio.</summary>
    public string? KeyDulFio { get; set; }

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата завершения обработки.</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>IP-адрес источника вызова.</summary>
    public string? SourceIp { get; set; }
}
