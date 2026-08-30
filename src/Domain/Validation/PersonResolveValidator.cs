using Mnemonios.Domain.DTOs;

namespace Mnemonios.Domain.Validation;

/// <summary>
/// Validation result containing success flag and error messages.
/// </summary>
public record ValidationResult
{
    /// <summary>Whether validation passed.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>List of validation error messages.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// Server-side validator for person resolve requests.
/// Static class — no dependencies on DB, DI, or HTTP context.
/// </summary>
public static class PersonResolveValidator
{
    /// <summary>
    /// Validates blocking errors (request must be rejected).
    /// </summary>
    public static ValidationResult Validate(ResolveRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.LastName))
            errors.Add("Обязательное поле «Фамилия».");

        if (string.IsNullOrWhiteSpace(request.FirstName))
            errors.Add("Обязательное поле «Имя».");

        if (string.IsNullOrWhiteSpace(request.SourceSystemId))
            errors.Add("Обязательное поле «Идентификатор внешней системы».");

        if (string.IsNullOrWhiteSpace(request.ExternalPersonId))
            errors.Add("Обязательное поле «Идентификатор лица во внешней системе».");

        if (request.Evidence is not null)
        {
            if (!string.IsNullOrWhiteSpace(request.Evidence.DulType) && !DulClassifier.IsValidCode(request.Evidence.DulType))
                errors.Add($"Недопустимый код вида документа «{request.Evidence.DulType}». Допустимые коды: 03, 07, 08, 10, 11, 12, 13, 15, 18, 21, 23, 24, 91.");

            if (request.Evidence.DulType == DulClassifier.OtherDocumentCode && string.IsNullOrWhiteSpace(request.Evidence.DulTypeName))
                errors.Add("При коде документа 91 (иные документы) обязательно указание наименования документа в поле «DulTypeName».");
        }

        return new ValidationResult { Errors = errors };
    }

    /// <summary>
    /// Validates non-blocking defects (data quality issues, logged but not rejected).
    /// </summary>
    public static IReadOnlyList<DefectInfo> ValidateDefects(ResolveRequest request)
    {
        var defects = new List<DefectInfo>();

        if (request.Evidence is null)
            return defects;

        if (!string.IsNullOrWhiteSpace(request.Evidence.Inn) && !InnValidator.Validate(request.Evidence.Inn))
        {
            defects.Add(new DefectInfo
            {
                DefectType = "invalid_inn",
                DefectMessage = "Некорректный ИНН: неверная контрольная сумма.",
                FieldName = "inn"
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Evidence.Snils) && !SnilsValidator.Validate(request.Evidence.Snils))
        {
            defects.Add(new DefectInfo
            {
                DefectType = "invalid_snils",
                DefectMessage = "Некорректный СНИЛС: неверная контрольная сумма.",
                FieldName = "snils"
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Evidence.DulSeries) && string.IsNullOrWhiteSpace(request.Evidence.DulNumber))
        {
            defects.Add(new DefectInfo
            {
                DefectType = "dul_incomplete",
                DefectMessage = "ДУЛ неполный: указана серия без номера.",
                FieldName = "dulNumber"
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Evidence.DulNumber) && string.IsNullOrWhiteSpace(request.Evidence.DulSeries))
        {
            defects.Add(new DefectInfo
            {
                DefectType = "dul_incomplete",
                DefectMessage = "ДУЛ неполный: указан номер без серии.",
                FieldName = "dulSeries"
            });
        }

        return defects;
    }
}
