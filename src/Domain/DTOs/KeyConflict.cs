namespace Mnemonios.Domain.DTOs;

/// <summary>
/// Расхождение ключа идентификации между запросом и существующей голд-записью.
/// </summary>
/// <param name="KeyType">Тип ключа (inn, snils, dul).</param>
public record KeyConflict(string KeyType);
