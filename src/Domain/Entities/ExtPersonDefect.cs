namespace Mnemonios.Domain.Entities;

/// <summary>
/// Сырые данные дефектов из входящего запроса идентификации (staging).
/// </summary>
public class ExtPersonDefect
{
    /// <summary>Уникальный идентификатор staging-записи.</summary>
    public Guid Id { get; set; }

    /// <summary>Ссылка на staging-запись персоны.</summary>
    public Guid ExtPersonId { get; set; }

    /// <summary>Код типа дефекта.</summary>
    public string DefectType { get; set; } = string.Empty;

    /// <summary>Человекочитаемое описание дефекта.</summary>
    public string DefectMessage { get; set; } = string.Empty;

    /// <summary>Имя поля, вызвавшего дефект (опционально).</summary>
    public string? FieldName { get; set; }

    /// <summary>Исходное значение дефектного поля (опционально).</summary>
    public string? OriginalValue { get; set; }

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; set; }
}
