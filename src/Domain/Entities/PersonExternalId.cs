namespace Mnemonios.Domain.Entities;

/// <summary>
/// Ссылка между PersonID и внешним системным идентификатором.
/// </summary>
public class PersonExternalId
{
    /// <summary>Уникальный идентификатор.</summary>
    public Guid Id { get; set; }

    /// <summary>Ссылка на персону.</summary>
    public Guid MasterId { get; set; }

    /// <summary>Идентификатор внешней информационной системы.</summary>
    public string SourceSystemId { get; set; } = string.Empty;

    /// <summary>Идентификатор персоны во внешней системе.</summary>
    public string ExternalPersonId { get; set; } = string.Empty;

    /// <summary>Опциональный типизированный классификатор из внешней системы.</summary>
    public string? ExternalPersonType { get; set; }

    /// <summary>Ключ юридического лица (организационная единица).</summary>
    public string? OrganizationUnitKey { get; set; }

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата последнего обновления записи.</summary>
    public DateTime UpdatedAt { get; set; }
}
