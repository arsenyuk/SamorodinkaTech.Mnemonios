namespace SamorodinkaTech.Mnemonios.Steward.Services;

/// <summary>
/// Элемент списка истории разрешённых конфликтов.
/// </summary>
public record ReviewHistoryItem(
    Guid Id,
    Guid ReviewId,
    Guid PersonAId,
    Guid PersonBId,
    string SharedKeyType,
    string ConflictKeyType,
    string Resolution,
    string ResolvedBy,
    DateTime ResolvedAt,
    DateTime CreatedAt);
