using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Enums;
using Mnemonios.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mnemonios.IntegrationTests;

/// <summary>
/// Интеграционные тесты для эндпоинта /persons/resolve-by-hashes.
/// Проверяют идентификацию персон по предвычисленным HMAC-SHA256 хешам.
/// </summary>
public class PersonHashResolveEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private static int _uidCounter;
    private readonly HttpClient _client;
    private readonly IServiceProvider _services;

    public PersonHashResolveEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _services = factory.Services;
    }

    // =========================================================================
    // 1. Resolve by hashes — создание нового лица (только ИНН-хеш)
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_NewPersonByInn_ReturnsUnmatched()
    {
        var uid = $"{Guid.NewGuid():N}{Interlocked.Increment(ref _uidCounter):X8}"[..12];
        var innHash = await ComputeInnHashAsync(uid);

        var request = new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-new-{uid}",
            KeyInn = innHash
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve-by-hashes", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResolveResponse>();
        result.Should().NotBeNull();
        result!.Status.Should().Be(PersonMatchStatus.Unmatched);
        result.MasterId.Should().NotBeNull();
    }

    // =========================================================================
    // 2. Resolve by hashes — повторный запрос (тот же externalId)
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_SameExternalIdTwice_ReturnsMatched()
    {
        var uid = $"{Guid.NewGuid():N}{Interlocked.Increment(ref _uidCounter):X8}"[..12];
        var innHash = await ComputeInnHashAsync(uid);

        var request = new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-repeat-{uid}",
            KeyInn = innHash
        };

        var first = await PostResolveByHashes(request);
        var second = await PostResolveByHashes(request);

        second.Status.Should().Be(PersonMatchStatus.Matched);
        second.MasterId.Should().Be(first.MasterId);
    }

    // =========================================================================
    // 3. Resolve by hashes — совпадение по ИНН-хешу
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_MatchByInnHash_ReturnsMatched()
    {
        var uid = $"{Guid.NewGuid():N}{Interlocked.Increment(ref _uidCounter):X8}"[..12];
        var innHash = await ComputeInnHashAsync(uid);

        var first = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-inn1-{uid}",
            KeyInn = innHash
        });

        var second = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_ERP",
            ExternalPersonId = $"ext-hash-inn2-{uid}",
            KeyInn = innHash
        });

        second.Status.Should().Be(PersonMatchStatus.Matched);
        second.MasterId.Should().Be(first.MasterId);
    }

    // =========================================================================
    // 4. Resolve by hashes — совпадение по СНИЛС-хешу
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_MatchBySnilsHash_ReturnsMatched()
    {
        var uid = $"{Guid.NewGuid():N}{Interlocked.Increment(ref _uidCounter):X8}"[..12];
        var snilsHash = await ComputeSnilsHashAsync(uid);

        var first = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-snils1-{uid}",
            KeySnils = snilsHash
        });

        var second = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_HR",
            ExternalPersonId = $"ext-hash-snils2-{uid}",
            KeySnils = snilsHash
        });

        second.Status.Should().Be(PersonMatchStatus.Matched);
        second.MasterId.Should().Be(first.MasterId);
    }

    // =========================================================================
    // 5. Resolve by hashes — совпадение по ДУЛ-хешу
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_MatchByDulHash_ReturnsMatched()
    {
        var uid = $"{Guid.NewGuid():N}{Interlocked.Increment(ref _uidCounter):X8}"[..12];
        var dulHash = await ComputeDulHashAsync(uid);

        var first = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-dul1-{uid}",
            KeyDul = dulHash
        });

        var second = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_ERP",
            ExternalPersonId = $"ext-hash-dul2-{uid}",
            KeyDul = dulHash
        });

        second.Status.Should().Be(PersonMatchStatus.Matched);
        second.MasterId.Should().Be(first.MasterId);
    }

    // =========================================================================
    // 6. Resolve by hashes — нет хешей → 400
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_NoHashes_Returns400()
    {
        var request = new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-empty-{Guid.NewGuid():N}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve-by-hashes", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // 7. Resolve by hashes — составные ключи (inn_fio)
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_CompositeKeyInnFio_ReturnsMatched()
    {
        var uid = $"{Guid.NewGuid():N}{Interlocked.Increment(ref _uidCounter):X8}"[..12];
        var innFioHash = await ComputeInnFioHashAsync(uid);

        var first = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-comp1-{uid}",
            KeyInnFio = innFioHash
        });

        var second = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_ERP",
            ExternalPersonId = $"ext-hash-comp2-{uid}",
            KeyInnFio = innFioHash
        });

        second.Status.Should().Be(PersonMatchStatus.Matched);
        second.MasterId.Should().Be(first.MasterId);
    }

    // =========================================================================
    // 8. Resolve by hashes — несколько хешей в одном запросе
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_MultipleHashes_CreatesPerson()
    {
        var uid = $"{Guid.NewGuid():N}{Interlocked.Increment(ref _uidCounter):X8}"[..12];
        var innHash = await ComputeInnHashAsync(uid);
        var snilsHash = await ComputeSnilsHashAsync(uid);

        var request = new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-multi-{uid}",
            KeyInn = innHash,
            KeySnils = snilsHash
        };

        var result = await PostResolveByHashes(request);

        result.Status.Should().Be(PersonMatchStatus.Unmatched);
        result.MasterId.Should().NotBeNull();
    }

    // =========================================================================
    // 9. Resolve by hashes — Ambiguous (расхождение ключей)
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_KeyConflict_ReturnsAmbiguous()
    {
        var uid = $"{Guid.NewGuid():N}{Interlocked.Increment(ref _uidCounter):X8}"[..12];
        var innHashA = await ComputeInnHashAsync(uid);
        var innHashB = await ComputeInnHashAsync($"b{uid}");
        var snilsHashA = await ComputeSnilsHashAsync(uid);
        var snilsHashB = await ComputeSnilsHashAsync($"c{uid}");

        // Person A: ИНН_A + СНИЛС_A
        var personA = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-amb1-{uid}",
            KeyInn = innHashA,
            KeySnils = snilsHashA
        });
        personA.Status.Should().Be(PersonMatchStatus.Unmatched);

        // Person B: ИНН_B + СНИЛС_B
        var personB = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_ERP",
            ExternalPersonId = $"ext-hash-amb2-{uid}",
            KeyInn = innHashB,
            KeySnils = snilsHashB
        });
        personB.Status.Should().Be(PersonMatchStatus.Unmatched);

        // Запрос: ИНН_A → personA, СНИЛС_B → personB → Ambiguous
        // Оба кандидата имеют K>0 (ИНН_A не совпадает с personB, СНИЛС_B не совпадает с personA)
        var conflict = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_HR",
            ExternalPersonId = $"ext-hash-amb3-{uid}",
            KeyInn = innHashA,
            KeySnils = snilsHashB
        });

        conflict.Status.Should().Be(PersonMatchStatus.Ambiguous);
        conflict.MasterId.Should().NotBeNull();
        conflict.KeyConflicts.Should().NotBeEmpty();
    }

    // =========================================================================
    // 10. Resolve by hashes — внешний ID уже привязан → Matched
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_ExistingExternalId_ReturnsMatched()
    {
        var uid = $"{Guid.NewGuid():N}{Interlocked.Increment(ref _uidCounter):X8}"[..12];
        var innHash = await ComputeInnHashAsync(uid);
        var extId = $"ext-hash-exists-{uid}";

        // Первый запрос: создание
        var first = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = extId,
            KeyInn = innHash
        });
        first.Status.Should().Be(PersonMatchStatus.Unmatched);

        // Второй запрос: с другим хешем, но тем же externalId
        var snilsHash = await ComputeSnilsHashAsync(uid);
        var second = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = extId,
            KeyInn = innHash,
            KeySnils = snilsHash
        });

        second.Status.Should().Be(PersonMatchStatus.Matched);
        second.MasterId.Should().Be(first.MasterId);
    }

    // =========================================================================
    // 11. Resolve by hashes — two persons linked via different systems
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_SamePersonFromDifferentSystems_ReturnsMatched()
    {
        var uid = $"{Guid.NewGuid():N}{Interlocked.Increment(ref _uidCounter):X8}"[..12];
        var innHash = await ComputeInnHashAsync(uid);

        var crm = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-sys1-{uid}",
            KeyInn = innHash
        });

        var erp = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_ERP",
            ExternalPersonId = $"ext-hash-sys2-{uid}",
            KeyInn = innHash
        });

        erp.Status.Should().Be(PersonMatchStatus.Matched);
        erp.MasterId.Should().Be(crm.MasterId);
    }

    // =========================================================================
    // 12. Resolve by hashes — uniqueness of MasterId for different people
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_DifferentInnHashes_ReturnsDifferentMasterIds()
    {
        var uid = $"{Guid.NewGuid():N}{Interlocked.Increment(ref _uidCounter):X8}"[..12];
        var innHashA = await ComputeInnHashAsync(uid);
        var innHashB = await ComputeInnHashAsync($"b{uid}");

        var personA = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-uniq1-{uid}",
            KeyInn = innHashA
        });

        var personB = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-uniq2-{uid}",
            KeyInn = innHashB
        });

        personA.MasterId!.Value.Should().NotBe(personB.MasterId!.Value);
    }

    // =========================================================================
    // 13. Resolve by hashes — keys are saved to the person
    // =========================================================================

    [Fact]
    public async Task ResolveByHashes_KeysSaved_PersonHasIdentificationKeys()
    {
        var uid = $"{Guid.NewGuid():N}{Interlocked.Increment(ref _uidCounter):X8}"[..12];
        var innHash = await ComputeInnHashAsync(uid);
        var snilsHash = await ComputeSnilsHashAsync(uid);

        var result = await PostResolveByHashes(new HashResolveRequest
        {
            SourceSystemId = "HASH_CRM",
            ExternalPersonId = $"ext-hash-keys-{uid}",
            KeyInn = innHash,
            KeySnils = snilsHash
        });

        result.MasterId.Should().NotBeNull();

        var response = await _client.GetAsync($"/persons/{result.MasterId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var person = await response.Content.ReadFromJsonAsync<PersonDto>();
        person.Should().NotBeNull();
        person!.IdentificationKeys.Should().NotBeEmpty();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<ResolveResponse> PostResolveByHashes(HashResolveRequest request)
    {
        var response = await _client.PostAsJsonAsync("/persons/resolve-by-hashes", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResolveResponse>())!;
    }

    /// <summary>
    /// Вычисляет HMAC-SHA256 для уникального ИНН на основе uid.
    /// </summary>
    private async Task<string> ComputeInnHashAsync(string uid)
    {
        // Генерируем уникальный 10-значный ИНН на основе символов uid (без коллизий GetHashCode)
        var hex = uid.Replace("-", "").ToLowerInvariant();
        while (hex.Length < 9)
            hex += hex;
        var nineDigits = hex[..9]
            .Select(c => c >= '0' && c <= '9' ? c - '0' : c - 'a' + 10)
            .Select(d => d % 10)
            .ToArray();
        int[] weights = [2, 4, 10, 3, 5, 9, 4, 6, 8];
        int sum = 0;
        for (int i = 0; i < 9; i++)
            sum += nineDigits[i] * weights[i];
        int checkDigit = sum % 11 % 10;
        var inn = string.Concat(nineDigits.Select(d => d.ToString())) + checkDigit;

        return await ComputeHmacAsync(inn);
    }

    /// <summary>
    /// Вычисляет HMAC-SHA256 для уникального СНИЛС на основе uid.
    /// </summary>
    private async Task<string> ComputeSnilsHashAsync(string uid)
    {
        var hex = uid.Replace("-", "").ToLowerInvariant();
        while (hex.Length < 9)
            hex += hex;
        var baseDigits = hex[..9]
            .Select(c => c >= '0' && c <= '9' ? c - '0' : c - 'a' + 10)
            .Select(d => d % 10)
            .ToArray();
        int[] weights = [9, 8, 7, 6, 5, 4, 3, 2, 1];
        int sum = 0;
        for (int i = 0; i < 9; i++)
            sum += baseDigits[i] * weights[i];
        int check = sum % 101;
        if (check == 100) check = 0;
        var snils = string.Concat(baseDigits.Select(d => d.ToString())) + check / 10 + check % 10;

        return await ComputeHmacAsync(snils);
    }

    /// <summary>
    /// Вычисляет HMAC-SHA256 для уникального ДУЛ на основе uid.
    /// </summary>
    private async Task<string> ComputeDulHashAsync(string uid)
    {
        var dul = $"ПАСПОРТ|{uid[..4]}|{uid[4..8]}";
        return await ComputeHmacAsync(dul);
    }

    /// <summary>
    /// Вычисляет HMAC-SHA256 для составного ключа inn_fio.
    /// </summary>
    private async Task<string> ComputeInnFioHashAsync(string uid)
    {
        var innHash = await ComputeInnHashAsync(uid);
        // inn_fio = HMAC(inn|lastName|firstName), но для теста генерируем уникальную строку
        var innFio = $"{innHash}|ФАМИЛИЯ_{uid}|ИМЯ_{uid}";
        return await ComputeHmacAsync(innFio);
    }

    /// <summary>
    /// Вычисляет HMAC-SHA256 для произвольной строки (для тестов с уникальными значениями).
    /// </summary>
    private async Task<string> ComputeRawHashAsync(string value)
    {
        return await ComputeHmacAsync(value);
    }

    private async Task<string> ComputeHmacAsync(string normalizedValue)
    {
        using var scope = _services.CreateScope();
        var hmacSettings = scope.ServiceProvider.GetRequiredService<IOptions<HmacSettings>>().Value;
        var normalizationService = scope.ServiceProvider.GetRequiredService<INormalizationService>();

        var valueToHash = normalizationService.NormalizeName(normalizedValue);

        var hmacKey = Encoding.UTF8.GetBytes(hmacSettings.Key);
        using var hmac = new HMACSHA256(hmacKey);
        var bytes = Encoding.UTF8.GetBytes(valueToHash);
        var hash = hmac.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
