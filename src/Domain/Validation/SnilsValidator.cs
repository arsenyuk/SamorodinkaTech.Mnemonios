namespace Mnemonios.Domain.Validation;

/// <summary>
/// Validator for Russian SNILS (Individual Insurance Number).
/// Supports 11-digit format with checksum verification.
/// </summary>
public static class SnilsValidator
{
    private const int Length = 11;

    private static readonly int[] Weights = [9, 8, 7, 6, 5, 4, 3, 2, 1];

    /// <summary>
    /// Validates SNILS format and checksum.
    /// Проверка контрольного числа проводится только для номеров > 001-001-998.
    /// Номера от 0 до 001-001-998 зарезервированы и не имеют валидной контрольной суммы.
    /// </summary>
    /// <param name="snils">SNILS string (11 digits, separators allowed).</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool Validate(string snils)
    {
        if (string.IsNullOrWhiteSpace(snils))
            return false;

        var digits = ExtractDigits(snils);
        if (digits.Length != Length)
            return false;

        // Номера 0 .. 001-001-998 (1001998) зарезервированы — контрольное число не проверяется
        var firstNine = int.Parse(new string(digits[..9]));
        if (firstNine <= 1_001_998)
            return true;

        var checkSum = ComputeCheckSum(digits[..9]);
        var actualCheck = (digits[9] - '0') * 10 + (digits[10] - '0');
        return checkSum == actualCheck;
    }

    private static char[] ExtractDigits(string value)
    {
        return value.Where(char.IsDigit).ToArray();
    }

    private static int ComputeCheckSum(char[] firstNine)
    {
        int sum = 0;
        for (int i = 0; i < Weights.Length; i++)
            sum += (firstNine[i] - '0') * Weights[i];

        int check = sum % 101;
        if (check == 100)
            check = 0;

        return check;
    }
}
