namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Информация о дефекте в ответе идентификации.
/// Не содержит ПДн.
/// </summary>
public record DefectInfo
{
    /// <summary>Тип дефекта (invalid_inn, invalid_snils, dul_incomplete).</summary>
    public required string DefectType { get; init; }

    /// <summary>Человекочитаемое описание дефекта.</summary>
    public required string DefectMessage { get; init; }

    /// <summary>Имя поля, вызвавшего дефект (опционально).</summary>
    public string? FieldName { get; init; }
}
