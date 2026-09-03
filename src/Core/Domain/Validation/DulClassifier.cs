using Mnemonios.Domain.DTOs;

namespace Mnemonios.Domain.Validation;

/// <summary>
/// Static classifier for identity document types (DUL) based on FNS Order from 31.08.2020 № ЕД-7-14/617@.
/// </summary>
public static class DulClassifier
{
    private const string CategoryCitizensRf = "Граждане РФ";
    private const string CategoryForeign = "Иностранные граждане";
    private const string CategoryOther = "Иные документы";

    /// <summary>Code for "other documents" requiring manual name entry.</summary>
    public const string OtherDocumentCode = "91";

    private static readonly IReadOnlyList<DulClassifierEntry> Entries =
    [
        new() { Code = "21", Name = "Паспорт гражданина Российской Федерации", Category = CategoryCitizensRf },
        new() { Code = "03", Name = "Свидетельство о рождении", Category = CategoryCitizensRf },
        new() { Code = "07", Name = "Военный билет", Category = CategoryCitizensRf },
        new() { Code = "08", Name = "Временное удостоверение, выданное взамен военного билета", Category = CategoryCitizensRf },
        new() { Code = "24", Name = "Удостоверение личности военнослужащего Российской Федерации", Category = CategoryCitizensRf },
        new() { Code = "10", Name = "Паспорт иностранного гражданина", Category = CategoryForeign },
        new() { Code = "12", Name = "Вид на жительство в Российской Федерации", Category = CategoryForeign },
        new() { Code = "15", Name = "Разрешение на временное проживание в Российской Федерации", Category = CategoryForeign },
        new() { Code = "11", Name = "Свидетельство о рассмотрении ходатайства о признании лица беженцем", Category = CategoryForeign },
        new() { Code = "13", Name = "Удостоверение беженца", Category = CategoryForeign },
        new() { Code = "18", Name = "Свидетельство о предоставлении временного убежища", Category = CategoryForeign },
        new() { Code = "23", Name = "Свидетельство о рождении, выданное уполномоченным органом иностранного государства", Category = CategoryForeign },
        new() { Code = "91", Name = "Иные документы", Category = CategoryOther }
    ];

    /// <summary>
    /// Checks whether the given code is a valid DUL classifier code.
    /// </summary>
    public static bool IsValidCode(string? code) =>
        code is not null && Entries.Any(e => e.Code == code);

    /// <summary>
    /// Returns the full classifier with flat list and grouped by category.
    /// </summary>
    public static DulClassifierResponse GetClassifier() => new()
    {
        Entries = Entries,
        ByCategory = Entries
            .GroupBy(e => e.Category)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<DulClassifierEntry>)g.ToList())
    };
}
