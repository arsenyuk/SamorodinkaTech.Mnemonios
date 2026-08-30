namespace Mnemonios.Domain.Validation;

/// <summary>
/// Validator for Russian INN (Taxpayer Identification Number).
/// Supports both 10-digit (legal entities) and 12-digit (individuals) formats.
/// </summary>
public static class InnValidator
{
    private const int LengthLegal = 10;
    private const int LengthIndividual = 12;

    private static readonly int[] WeightsLegal = [2, 4, 10, 3, 5, 9, 4, 6, 8];
    private static readonly int[] WeightsIndividual1 = [7, 2, 4, 10, 3, 5, 9, 4, 6, 8];
    private static readonly int[] WeightsIndividual2 = [3, 7, 2, 4, 10, 3, 5, 9, 4, 6, 8];

    /// <summary>
    /// Validates INN format and checksum.
    /// </summary>
    /// <param name="inn">INN string (10 or 12 digits, separators allowed).</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool Validate(string inn)
    {
        if (string.IsNullOrWhiteSpace(inn))
            return false;

        var digits = ExtractDigits(inn);

        if (digits.Length == LengthLegal)
            return ValidateLegal(digits);

        if (digits.Length == LengthIndividual)
            return ValidateIndividual(digits);

        return false;
    }

    private static bool ValidateLegal(char[] digits)
    {
        var expectedCheck = ComputeWeightedSum(digits[..9], WeightsLegal);
        return (digits[9] - '0') == expectedCheck;
    }

    private static bool ValidateIndividual(char[] digits)
    {
        var checkDigit1 = ComputeWeightedSum(digits[..10], WeightsIndividual1);
        if ((digits[10] - '0') != checkDigit1)
            return false;

        var expectedCheck2 = ComputeWeightedSum(digits[..11], WeightsIndividual2);
        return (digits[11] - '0') == expectedCheck2;
    }

    private static char[] ExtractDigits(string value)
    {
        return value.Where(char.IsDigit).ToArray();
    }

    private static int ComputeWeightedSum(char[] digits, int[] weights)
    {
        int sum = 0;
        for (int i = 0; i < weights.Length; i++)
            sum += (digits[i] - '0') * weights[i];

        return sum % 11 % 10;
    }
}
