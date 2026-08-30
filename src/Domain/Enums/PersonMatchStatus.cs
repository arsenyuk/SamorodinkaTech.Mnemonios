namespace Mnemonios.Domain.Enums;

/// <summary>
/// Result status of person identification/resolution.
/// </summary>
public enum PersonMatchStatus
{
    /// <summary>Найдено однозначное соответствие — возвращён существующий PersonID.</summary>
    Matched,

    /// <summary>Соответствие не найдено — создан новый PersonID.</summary>
    Unmatched,

    /// <summary>Частичные или неоднозначные данные — требуется дополнительный анализ.</summary>
    Ambiguous,

    /// <summary>Conflicting data — keys point to different persons.</summary>
    Conflict
}
