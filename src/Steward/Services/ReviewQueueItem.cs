namespace SamorodinkaTech.Mnemonios.Steward.Services;

/// <summary>
/// DTO для отображения записи очереди на обработку.
/// </summary>
public record ReviewQueueItem(
    Guid Id,
    Guid PersonAId,
    Guid PersonBId,
    string SharedKeyType,
    string ConflictKeyType,
    DateTime CreatedAt);
