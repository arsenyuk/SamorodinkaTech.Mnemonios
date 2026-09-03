using System.Globalization;
using System.Text;

namespace Mnemonios.Infrastructure.Services;

/// <summary>
/// Normalization rules for person data fields.
/// </summary>
public interface INormalizationService
{
    /// <summary>Нормализует поле имени: trim, схлопывание пробелов, NFC, верхний регистр.</summary>
    string NormalizeName(string value);

    /// <summary>Нормализует ИНН: trim, схлопывание пробелов, NFC, верхний регистр.</summary>
    string NormalizeInn(string value);

    /// <summary>Нормализует СНИЛС: trim, схлопывание пробелов, NFC, верхний регистр.</summary>
    string NormalizeSnils(string value);

    /// <summary>Нормализует ДУЛ: объединяет тип + серию + номер в одну нормализованную строку.</summary>
    string NormalizeDul(string type, string series, string number);

    /// <summary>Нормализует серию/номер ДУЛ: удаление пробелов, верхний регистр.</summary>
    string NormalizeDulField(string value);
}

/// <summary>
/// Standard normalization for person data fields.
/// Rules: trim whitespace, collapse multiple spaces, Unicode NFC, ToUpperInvariant.
/// </summary>
public class NormalizationService : INormalizationService
{
    /// <inheritdoc/>
    public string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        var collapsed = CollapseSpaces(trimmed);
        var nfc = collapsed.Normalize(NormalizationForm.FormC);
        return nfc.ToUpperInvariant();
    }

    /// <inheritdoc/>
    public string NormalizeInn(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return NormalizeName(value);
    }

    /// <inheritdoc/>
    public string NormalizeSnils(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return NormalizeName(value);
    }

    /// <inheritdoc/>
    public string NormalizeDul(string type, string series, string number)
    {
        var normalizedType = NormalizeName(type ?? string.Empty);
        var normalizedSeries = NormalizeName(series ?? string.Empty);
        var normalizedNumber = NormalizeName(number ?? string.Empty);

        var combined = $"{normalizedType}|{normalizedSeries}|{normalizedNumber}";
        return combined.ToUpperInvariant();
    }

    /// <inheritdoc/>
    public string NormalizeDulField(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Replace(" ", string.Empty).ToUpperInvariant();
    }

    private static string CollapseSpaces(string value)
    {
        var sb = new StringBuilder(value.Length);
        bool prevSpace = false;

        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!prevSpace)
                    sb.Append(' ');
                prevSpace = true;
            }
            else
            {
                sb.Append(c);
                prevSpace = false;
            }
        }

        return sb.ToString().Trim();
    }
}
