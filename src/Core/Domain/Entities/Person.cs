namespace Mnemonios.Domain.Entities;

/// <summary>
/// Единая запись физического лица в MPI.
/// Хранит только хеши и таймстемпы — без ПДн.
/// </summary>
public class Person
{
    /// <summary>Уникальный мастер-идентификатор.</summary>
    public Guid MasterId { get; set; }

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата последнего обновления записи.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>HMAC-ключи идентификации для детерминированного сопоставления.</summary>
    public ICollection<PersonIdentificationKey> IdentificationKeys { get; set; } = [];

    /// <summary>Ссылки на внешние информационные системы.</summary>
    public ICollection<PersonExternalId> ExternalIds { get; set; } = [];

    /// <summary>Дефекты данных, обнаруженные при идентификации.</summary>
    public ICollection<PersonDefect> Defects { get; set; } = [];

    /// <summary>Записи отложенной прекращения обработки ПДн.</summary>
    public ICollection<PersonDeferredCessation> DeferredCessations { get; set; } = [];

    /// <summary>Документы ДУЛ (тип + хеш).</summary>
    public ICollection<PersonDocument> Documents { get; set; } = [];
}
