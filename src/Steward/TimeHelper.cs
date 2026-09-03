namespace SamorodinkaTech.Mnemonios.Steward;

/// <summary>
/// Хелпер для конвертации UTC в локальное время.
/// </summary>
public static class TimeHelper
{
    private static readonly TimeZoneInfo LocalTimeZone = TimeZoneInfo.Local;

    /// <summary>
    /// Конвертировать UTC DateTime в локальное время.
    /// </summary>
    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, LocalTimeZone);

    /// <summary>
    /// Форматировать UTC DateTime как локальную строку.
    /// </summary>
    public static string ToLocalString(DateTime utc, string format = "dd.MM.yyyy HH:mm:ss") =>
        ToLocal(utc).ToString(format);
}
