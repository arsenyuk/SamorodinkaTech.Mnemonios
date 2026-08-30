using System.Security.Cryptography;
using System.Text;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Entities;
using Mnemonios.Domain.Validation;
using Microsoft.Extensions.Options;

namespace Mnemonios.Infrastructure.Services;

/// <summary>
/// HMAC key configuration.
/// </summary>
public class HmacSettings
{
    /// <summary>Секретный ключ для вычисления HMAC-SHA256.</summary>
    public string Key { get; set; } = string.Empty;
}

/// <summary>
/// Computed identification key.
/// </summary>
public record IdentificationKey
{
    /// <summary>Тип ключа (inn, snils, dul, fio, fio_full, inn_fio, snils_fio, dul_fio).</summary>
    public required string KeyType { get; init; }

    /// <summary>HMAC-SHA256 hex-строка.</summary>
    public required string KeyValue { get; init; }
}

/// <summary>
/// Service for computing HMAC-SHA256 identification keys from normalized person data.
/// </summary>
public interface IIdentificationKeyService
{
    /// <summary>
    /// Computes all available identification keys from a resolve request.
    /// </summary>
    IReadOnlyList<IdentificationKey> ComputeKeys(ResolveRequest request, int normalizationVersion = 1);
}

/// <summary>
/// Implementation of identification key computation using HMAC-SHA256.
/// </summary>
public class IdentificationKeyService : IIdentificationKeyService
{
    private const int DefaultNormalizationVersion = 1;

    private readonly byte[] _hmacKey;
    private readonly INormalizationService _normalizationService;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="IdentificationKeyService"/>.
    /// </summary>
    public IdentificationKeyService(
        IOptions<HmacSettings> hmacSettings,
        INormalizationService normalizationService)
    {
        _hmacKey = Encoding.UTF8.GetBytes(hmacSettings.Value.Key);
        _normalizationService = normalizationService ?? throw new ArgumentNullException(nameof(normalizationService));
    }

    /// <inheritdoc/>
    public IReadOnlyList<IdentificationKey> ComputeKeys(ResolveRequest request, int normalizationVersion = DefaultNormalizationVersion)
    {
        var keys = new List<IdentificationKey>();

        var evidence = request.Evidence;
        var normalizedInn = NormalizeInnIfValid(evidence?.Inn);
        var normalizedSnils = NormalizeSnilsIfValid(evidence?.Snils);
        var normalizedDul = NormalizeDulIfPresent(evidence?.DulType, evidence?.DulSeries, evidence?.DulNumber);
        var normalizedLastName = _normalizationService.NormalizeName(request.LastName);
        var normalizedFirstName = _normalizationService.NormalizeName(request.FirstName);
        var normalizedMiddleName = !string.IsNullOrWhiteSpace(request.MiddleName)
            ? _normalizationService.NormalizeName(request.MiddleName)
            : null;

        if (normalizedInn is not null)
            keys.Add(CreateKey("inn", normalizedInn));

        if (normalizedSnils is not null)
            keys.Add(CreateKey("snils", normalizedSnils));

        if (normalizedDul is not null)
            keys.Add(CreateKey("dul", normalizedDul));

        var fio = $"{normalizedLastName}|{normalizedFirstName}";
        keys.Add(CreateKey("fio", fio));

        if (normalizedMiddleName is not null)
        {
            var fioFull = $"{normalizedLastName}|{normalizedFirstName}|{normalizedMiddleName}";
            keys.Add(CreateKey("fio_full", fioFull));
        }

        if (normalizedInn is not null)
        {
            var innFio = $"{normalizedInn}|{fio}";
            keys.Add(CreateKey("inn_fio", innFio));
        }

        if (normalizedSnils is not null)
        {
            var snilsFio = $"{normalizedSnils}|{fio}";
            keys.Add(CreateKey("snils_fio", snilsFio));
        }

        if (normalizedDul is not null)
        {
            var dulFio = $"{normalizedDul}|{fio}";
            keys.Add(CreateKey("dul_fio", dulFio));
        }

        return keys;
    }

    private IdentificationKey CreateKey(string keyType, string normalizedValue)
    {
        var hash = ComputeHmacSha256(normalizedValue);
        return new IdentificationKey
        {
            KeyType = keyType,
            KeyValue = hash
        };
    }

    private string ComputeHmacSha256(string value)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = hmac.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string? NormalizeInnIfValid(string? inn)
    {
        if (string.IsNullOrWhiteSpace(inn))
            return null;

        var normalized = _normalizationService.NormalizeInn(inn);
        if (normalized.Length != 10 && normalized.Length != 12)
            return null;

        if (!InnValidator.Validate(inn))
            return null;

        return normalized;
    }

    private string? NormalizeSnilsIfValid(string? snils)
    {
        if (string.IsNullOrWhiteSpace(snils))
            return null;

        var normalized = _normalizationService.NormalizeSnils(snils);
        if (normalized.Length != 11)
            return null;

        if (!SnilsValidator.Validate(snils))
            return null;

        return normalized;
    }

    private string? NormalizeDulIfPresent(string? type, string? series, string? number)
    {
        if (string.IsNullOrWhiteSpace(series) || string.IsNullOrWhiteSpace(number))
            return null;

        return _normalizationService.NormalizeDul(type ?? string.Empty, series, number);
    }
}
