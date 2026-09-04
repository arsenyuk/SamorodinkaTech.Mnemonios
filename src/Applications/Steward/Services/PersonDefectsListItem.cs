namespace SamorodinkaTech.Mnemonios.Steward.Services;

/// <summary>
/// Элемент списка мастер-записей с дефектами.
/// </summary>
public record PersonDefectsListItem(
    Guid MasterId,
    DateTime CreatedAt,
    int DefectCount,
    IReadOnlyList<string> DefectTypes);
