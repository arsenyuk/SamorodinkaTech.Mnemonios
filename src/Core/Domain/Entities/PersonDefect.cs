namespace Mnemonios.Domain.Entities;

/// <summary>
/// Дефект данных персоны, обнаруженный при идентификации (без ПДн).
/// </summary>
public class PersonDefect
{
    /// <summary>Уникальный идентификатор дефекта.</summary>
    public Guid Id { get; set; }

    /// <summary>Ссылка на персону.</summary>
    public Guid MasterId { get; set; }

    /// <summary>Тип дефекта (invalid_inn, invalid_snils, dul_incomplete).</summary>
    public string DefectType { get; set; } = string.Empty;

    /// <summary>Человекочитаемое описание дефекта.</summary>
    public string DefectMessage { get; set; } = string.Empty;

    /// <summary>Имя поля, вызвавшего дефект (опционально).</summary>
    public string? FieldName { get; set; }

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }
}
