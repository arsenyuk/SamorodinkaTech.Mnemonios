namespace Mnemonios.Domain.Entities;

/// <summary>
/// Запись документа ДУЛ (тип + хеш, без ПДн).
/// </summary>
public class PersonDocument
{
    /// <summary>Уникальный идентификатор документа.</summary>
    public Guid Id { get; set; }

    /// <summary>Ссылка на мастер-запись.</summary>
    public Guid MasterId { get; set; }

    /// <summary>Код типа документа (21, 10, 91 и т.д.).</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 хеш нормализованных данных ДУЛ.</summary>
    public string DocumentHash { get; set; } = string.Empty;

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }
}
