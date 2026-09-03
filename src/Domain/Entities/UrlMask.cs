namespace Mnemonios.Domain.Entities;

/// <summary>
/// URL-маска для триады (ЮЛ, Система, Тип объекта).
/// Используется для формирования ссылки на информационный объект во внешней системе.
/// </summary>
public class UrlMask
{
    /// <summary>Уникальный идентификатор.</summary>
    public Guid Id { get; set; }

    /// <summary>Ключ юридического лица (пустая строка = без ЮЛ).</summary>
    public string OrganizationUnitKey { get; set; } = string.Empty;

    /// <summary>Идентификатор системы-источника.</summary>
    public string SourceSystemId { get; set; } = string.Empty;

    /// <summary>Тип объекта во внешней системе (пустая строка = без типа).</summary>
    public string ExternalPersonType { get; set; } = string.Empty;

    /// <summary>
    /// Шаблон URL. Плейсхолдеры: {external_person_id}, {source_system_id}, {organization_unit_key}.
    /// Пример: https://crm.example.com/employee/{external_person_id}
    /// </summary>
    public string UrlPattern { get; set; } = string.Empty;

    /// <summary>Дата создания.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата обновления.</summary>
    public DateTime UpdatedAt { get; set; }
}
