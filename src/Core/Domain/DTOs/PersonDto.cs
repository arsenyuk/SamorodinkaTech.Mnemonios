namespace Mnemonios.Domain.DTOs;

/// <summary>
/// DTO данных персоны для GET-ответов.
/// Не содержит ПДн — только идентификаторы и метаданные.
/// </summary>
public record PersonDto
{
    /// <summary>Идентификатор персоны.</summary>
    public Guid MasterId { get; init; }

    /// <summary>Дата создания записи.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Внешние ссылки.</summary>
    public IReadOnlyList<ExternalIdentifierDto> Identifiers { get; init; } = [];

    /// <summary>Дефекты данных.</summary>
    public IReadOnlyList<DefectInfo> Defects { get; init; } = [];

    /// <summary>HMAC-ключи идентификации для детерминированного сопоставления.</summary>
    public IReadOnlyList<IdentificationKeyDto> IdentificationKeys { get; init; } = [];

    /// <summary>Записи отложенной прекращения обработки ПДн.</summary>
    public IReadOnlyList<DeferredCessationDto> DeferredCessations { get; init; } = [];
}
