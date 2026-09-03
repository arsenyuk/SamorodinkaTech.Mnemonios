namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Результат операции прекращения обработки ПДн.
/// </summary>
public record CessationResponse
{
    /// <summary>Internal person identifier whose data was deleted (null if no person found).</summary>
    public Guid? MasterId { get; init; }

    /// <summary>Количество удалённых ключей идентификации.</summary>
    public int DeletedKeys { get; init; }

    /// <summary>Количество удалённых внешних ссылок.</summary>
    public int DeletedExternalIds { get; init; }

    /// <summary>Количество удалённых записей дефектов.</summary>
    public int DeletedDefects { get; init; }
}
