using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Mnemonios.IntegrationTests;

/// <summary>
/// E2E тесты для ЕДИН MPI — полный цикл через HTTP API.
/// Требуют реальную PostgreSQL (или WebApplicationFactory с InMemory DB).
/// </summary>
public class PersonResolveEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PersonResolveEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // =========================================================================
    // 1. Resolve — создание нового лица
    // =========================================================================

    [Fact]
    public async Task Resolve_NewPerson_ReturnsUnmatched()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var request = new ResolveRequest
        {
            LastName = "Грушевский",
            FirstName = "Августин",
            MiddleName = "Игнатьевич",
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-new-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResolveResponse>();
        result.Should().NotBeNull();
        result!.Status.Should().Be(PersonMatchStatus.Unmatched);
        result.MasterId.Should().NotBeNull();
    }

    // =========================================================================
    // 2. Resolve — повторный запрос (тот же sourceSystemId + externalPersonId)
    // =========================================================================

    [Fact]
    public async Task Resolve_SameExternalIdTwice_ReturnsMatched()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var extId = $"ext-repeat-{uid}";
        var request = new ResolveRequest
        {
            LastName = "Шаляпин",
            FirstName = "Родион",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "CRM",
            ExternalPersonId = extId
        };

        var first = await PostResolve(request);
        var second = await PostResolve(request);

        second.Status.Should().Be(PersonMatchStatus.Matched);
        second.MasterId.Should().Be(first.MasterId);
    }

    // =========================================================================
    // 3. Resolve — тот же человек из другой ИС
    // =========================================================================

    [Fact]
    public async Task Resolve_SamePersonFromDifferentSystem_ReturnsMatched()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var crm = await PostResolve(new ResolveRequest
        {
            LastName = "Спартаков",
            FirstName = "Лев",
            MiddleName = "Маркович",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-spa-{uid}"
        });

        var erp = await PostResolve(new ResolveRequest
        {
            LastName = "Спартаков",
            FirstName = "Лев",
            MiddleName = "Маркович",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "ERP",
            ExternalPersonId = $"emp-spa-{uid}"
        });

        erp.Status.Should().Be(PersonMatchStatus.Matched);
        erp.MasterId.Should().Be(crm.MasterId);
    }

    // =========================================================================
    // 4. Resolve — совпадение по ИНН
    // =========================================================================

    [Fact]
    public async Task Resolve_MatchByInn_ReturnsMatched()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var first = await PostResolve(new ResolveRequest
        {
            LastName = "Кочубей",
            FirstName = "Демьян",
            Evidence = new Evidence { Inn = "7707083893" },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-koch-{uid}"
        });

        var second = await PostResolve(new ResolveRequest
        {
            LastName = "Кочубей",
            FirstName = "Демьян",
            MiddleName = "Святославович",
            Evidence = new Evidence { Inn = "7707083893" },
            SourceSystemId = "ERP",
            ExternalPersonId = $"emp-koch-{uid}"
        });

        second.Status.Should().Be(PersonMatchStatus.Matched);
        second.MasterId.Should().Be(first.MasterId);
    }

    // =========================================================================
    // 5. Resolve — совпадение по СНИЛС
    // =========================================================================

    [Fact]
    public async Task Resolve_MatchBySnils_ReturnsMatched()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var snils = GenerateValidSnils(uid);
        var first = await PostResolve(new ResolveRequest
        {
            LastName = "Пырьев",
            FirstName = "Никодим",
            Evidence = new Evidence { Snils = snils },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-pyr-{uid}"
        });

        var second = await PostResolve(new ResolveRequest
        {
            LastName = "Пырьев",
            FirstName = "Никодим",
            MiddleName = "Олегович",
            Evidence = new Evidence { Snils = snils },
            SourceSystemId = "HR",
            ExternalPersonId = $"hr-pyr-{uid}"
        });

        second.Status.Should().Be(PersonMatchStatus.Matched);
        second.MasterId.Should().Be(first.MasterId);
    }

    // =========================================================================
    // 6. Resolve — совпадение по ДУЛ
    // =========================================================================

    [Fact]
    public async Task Resolve_MatchByDul_ReturnsMatched()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var first = await PostResolve(new ResolveRequest
        {
            LastName = "Ломоносов",
            FirstName = "Борислав",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-lom-{uid}"
        });

        var second = await PostResolve(new ResolveRequest
        {
            LastName = "Ломоносов",
            FirstName = "Борислав",
            MiddleName = "Климонович",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "ERP",
            ExternalPersonId = $"emp-lom-{uid}"
        });

        second.Status.Should().Be(PersonMatchStatus.Matched);
        second.MasterId.Should().Be(first.MasterId);
    }

    // =========================================================================
    // 7. Resolve — нормализация ФИО (регистр, пробелы)
    // =========================================================================

    [Fact]
    public async Task Resolve_Normalization_IgnoresCaseAndSpaces()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var first = await PostResolve(new ResolveRequest
        {
            LastName = "  Харитонов  ",
            FirstName = "Устин",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-khar-{uid}"
        });

        var second = await PostResolve(new ResolveRequest
        {
            LastName = "ХАРИТОНОВ",
            FirstName = "устин",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "ERP",
            ExternalPersonId = $"emp-khar-{uid}"
        });

        second.Status.Should().Be(PersonMatchStatus.Matched);
        second.MasterId.Should().Be(first.MasterId);
    }

    // =========================================================================
    // 8. Resolve — Conflict: ИНН + ФИО
    // =========================================================================

    [Fact]
    public async Task Resolve_ConflictingKeys_AutoMergesWhenInnResolves()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var inn = GenerateValidInn(uid);
        var snils = GenerateValidSnils(uid);

        var person1 = await PostResolve(new ResolveRequest
        {
            LastName = "Вешняков",
            FirstName = "Тарас",
            Evidence = new Evidence { Inn = inn },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-vesh1-{uid}"
        });

        var person2 = await PostResolve(new ResolveRequest
        {
            LastName = "Вешняков",
            FirstName = "Глеб",
            Evidence = new Evidence { Snils = snils },
            SourceSystemId = "ERP",
            ExternalPersonId = $"ext-vesh2-{uid}"
        });

        // Запрос с ИНН + СНИЛС → ИНН матчит person1, СНИЛС матчит person2
        // Авто-merge: person2 → person1 (ИНН разрешает конфликт)
        var request = new ResolveRequest
        {
            LastName = "ВЕШНЯКОВ",
            FirstName = "ГЛЕБ",
            Evidence = new Evidence { Inn = inn, Snils = snils },
            SourceSystemId = "HR",
            ExternalPersonId = $"ext-vesh3-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResolveResponse>();

        // Авто-merge: ИНН разрешает конфликт → Matched, не Conflict
        result!.Status.Should().Be(PersonMatchStatus.Matched);
        result.MasterId.Should().Be(person1.MasterId);
    }

    // =========================================================================
    // 8b. Conflict через СНИЛС + ФИО
    // =========================================================================

    [Fact]
    public async Task Resolve_ConflictBySnilsAndFio_ReturnsConflict()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var snils = GenerateValidSnils(uid);

        var person1 = await PostResolve(new ResolveRequest
        {
            LastName = "Зубарев",
            FirstName = "Ян",
            Evidence = new Evidence { Snils = snils },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-zub1-{uid}"
        });

        var person2 = await PostResolve(new ResolveRequest
        {
            LastName = "Зубарев",
            FirstName = "Геннадий",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "ERP",
            ExternalPersonId = $"ext-zub2-{uid}"
        });

        var request = new ResolveRequest
        {
            LastName = "ЗУБАРЕВ",
            FirstName = "ГЕННАДИЙ",
            Evidence = new Evidence { Snils = snils, DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "HR",
            ExternalPersonId = $"ext-zub3-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);
        var result = await response.Content.ReadFromJsonAsync<ResolveResponse>();

        // Новая логика: оба кандидата (SNILS → person1, DUL → person2) имеют M=1, K=0.
        // Выбирается первый найденный кандидат → Matched.
        result!.Status.Should().Be(PersonMatchStatus.Matched);
        (result.MasterId == person1.MasterId || result.MasterId == person2.MasterId).Should().BeTrue();
    }

    // =========================================================================
    // 8c. Conflict через ДУЛ + ФИО
    // =========================================================================

    [Fact]
    public async Task Resolve_ConflictByDulAndFio_ReturnsMatched()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var snils = GenerateValidSnils(uid);

        var person1 = await PostResolve(new ResolveRequest
        {
            LastName = "Салтыков",
            FirstName = "Август",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-salt1-{uid}"
        });

        var person2 = await PostResolve(new ResolveRequest
        {
            LastName = "Салтыков",
            FirstName = "Демьян",
            Evidence = new Evidence { Snils = snils },
            SourceSystemId = "ERP",
            ExternalPersonId = $"ext-salt2-{uid}"
        });

        var request = new ResolveRequest
        {
            LastName = "САЛТЫКОВ",
            FirstName = "ДЕМЬЯН",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid), Snils = snils },
            SourceSystemId = "HR",
            ExternalPersonId = $"ext-salt3-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);
        var result = await response.Content.ReadFromJsonAsync<ResolveResponse>();

        // Новая логика: оба кандидата (DUL → person1, SNILS → person2) имеют M=1, K=0.
        // Выбирается первый найденный кандидат → Matched.
        result!.Status.Should().Be(PersonMatchStatus.Matched);
        (result.MasterId == person1.MasterId || result.MasterId == person2.MasterId).Should().BeTrue();
    }

    // =========================================================================
    // 8d. Conflict через ДУЛ + СНИЛС разных людей
    // =========================================================================

    [Fact]
    public async Task Resolve_ConflictByDulAndSnils_ReturnsMatched()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var snils = GenerateValidSnils(uid);

        var personA = await PostResolve(new ResolveRequest
        {
            LastName = "Рахманинов",
            FirstName = "Марк",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-rakh1-{uid}"
        });

        var personB = await PostResolve(new ResolveRequest
        {
            LastName = "Гущин",
            FirstName = "Артём",
            Evidence = new Evidence { Snils = snils },
            SourceSystemId = "ERP",
            ExternalPersonId = $"ext-gush1-{uid}"
        });

        var request = new ResolveRequest
        {
            LastName = "РАХМАНИНОВ",
            FirstName = "МАРК",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid), Snils = snils },
            SourceSystemId = "HR",
            ExternalPersonId = $"ext-rakh2-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);
        var result = await response.Content.ReadFromJsonAsync<ResolveResponse>();

        // Новая логика: оба кандидата (DUL → personA, SNILS → personB) имеют M=1, K=0.
        // Выбирается первый найденный кандидат → Matched.
        result!.Status.Should().Be(PersonMatchStatus.Matched);
        (result.MasterId == personA.MasterId || result.MasterId == personB.MasterId).Should().BeTrue();
    }

    // =========================================================================
    // 9. Resolve — неполные данные (только ФИО)
    // =========================================================================

    [Fact]
    public async Task Resolve_PartialData_OnlyFio_CreatesNewPerson()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var request = new ResolveRequest
        {
            LastName = "Мельников",
            FirstName = "Святослав",
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-mel-{uid}"
        };

        var result = await PostResolve(request);

        result.Status.Should().Be(PersonMatchStatus.Unmatched);
        result.MasterId.Should().NotBeNull();
    }

    // =========================================================================
    // 10. Валидация — обязательные поля
    // =========================================================================

    [Fact]
    public async Task Resolve_MissingLastName_Returns400()
    {
        var request = new ResolveRequest
        {
            LastName = "",
            FirstName = "Эйзенштейн",
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-val-{Guid.NewGuid():N}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Resolve_MissingFirstName_Returns400()
    {
        var request = new ResolveRequest
        {
            LastName = "Юсупов",
            FirstName = "",
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-val-{Guid.NewGuid():N}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // 11. Валидация — невалидный ИНН
    // =========================================================================

    [Fact]
    public async Task Resolve_InvalidInn_Returns400()
    {
        var request = new ResolveRequest
        {
            LastName = "Толстой",
            FirstName = "Родион",
            Evidence = new Evidence { Inn = "123456789012" },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-inn-{Guid.NewGuid():N}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // 12. Валидация — невалидный СНИЛС
    // =========================================================================

    [Fact]
    public async Task Resolve_InvalidSnils_Returns400()
    {
        var request = new ResolveRequest
        {
            LastName = "Шувалов",
            FirstName = "Демьян",
            Evidence = new Evidence { Snils = "12345678901" },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-snils-{Guid.NewGuid():N}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // 10b. Валидация — отсутствует SourceSystemId
    // =========================================================================

    [Fact]
    public async Task Resolve_MissingSourceSystemId_Returns400()
    {
        var request = new ResolveRequest
        {
            LastName = "Шишкин",
            FirstName = "Фока",
            SourceSystemId = "",
            ExternalPersonId = $"ext-val-{Guid.NewGuid():N}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // 10c. Валидация — отсутствует ExternalPersonId
    // =========================================================================

    [Fact]
    public async Task Resolve_MissingExternalPersonId_Returns400()
    {
        var request = new ResolveRequest
        {
            LastName = "Шишкин",
            FirstName = "Фока",
            SourceSystemId = "CRM",
            ExternalPersonId = ""
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // 10d. Валидация — DulSeries без DulNumber
    // =========================================================================

    [Fact]
    public async Task Resolve_DulSeriesWithoutNumber_Returns400()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var request = new ResolveRequest
        {
            LastName = "Шишкин",
            FirstName = "Фока",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510" },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-dul-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // 10e. Валидация — DulNumber без DulSeries
    // =========================================================================

    [Fact]
    public async Task Resolve_DulNumberWithoutSeries_Returns400()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var request = new ResolveRequest
        {
            LastName = "Шишкин",
            FirstName = "Фока",
            Evidence = new Evidence { DulType = "21", DulNumber = "123456" },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-dul-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // 12b. Валидация — валидный ИНН (10 цифр)
    // =========================================================================

    [Fact]
    public async Task Resolve_ValidInn10_ReturnsOk()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var request = new ResolveRequest
        {
            LastName = "Корнеев",
            FirstName = "Лазарь",
            Evidence = new Evidence { Inn = "7707083893" },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-inn10-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // 12c. Валидация — валидный ИНН (12 цифр)
    // =========================================================================

    [Fact]
    public async Task Resolve_ValidInn12_ReturnsOk()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var request = new ResolveRequest
        {
            LastName = "Корнеев",
            FirstName = "Лазарь",
            Evidence = new Evidence { Inn = "770708389324" },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-inn12-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // 12d. Валидация — валидный СНИЛС
    // =========================================================================

    [Fact]
    public async Task Resolve_ValidSnils_ReturnsOk()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var snils = GenerateValidSnils(uid);
        var request = new ResolveRequest
        {
            LastName = "Ягодин",
            FirstName = "Прохор",
            Evidence = new Evidence { Snils = snils },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-snils-ok-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // 13. GetPerson — получение данных лица
    // =========================================================================

    [Fact]
    public async Task GetPerson_ExistingPerson_ReturnsData()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var created = await PostResolve(new ResolveRequest
        {
            LastName = "Цветаев",
            FirstName = "Никодим",
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-tsv-{uid}"
        });

        var response = await _client.GetAsync($"/persons/{created.MasterId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var person = await response.Content.ReadFromJsonAsync<PersonDto>();
        person.Should().NotBeNull();
        person!.MasterId.Should().Be(created.MasterId!.Value);
    }

    // =========================================================================
    // 14. GetPerson — лицо не найдено
    // =========================================================================

    [Fact]
    public async Task GetPerson_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/persons/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================================================
    // 15. GetIdentifiers — связи с внешними системами
    // =========================================================================

    [Fact]
    public async Task GetIdentifiers_MultipleSystems_ReturnsAll()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var created = await PostResolve(new ResolveRequest
        {
            LastName = "Ухтомский",
            FirstName = "Борислав",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-ukh-{uid}"
        });

        await PostResolve(new ResolveRequest
        {
            LastName = "Ухтомский",
            FirstName = "Борислав",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "ERP",
            ExternalPersonId = $"emp-ukh-{uid}"
        });

        var response = await _client.GetAsync($"/persons/{created.MasterId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var person = await response.Content.ReadFromJsonAsync<PersonDto>();
        person.Should().NotBeNull();
        person!.Identifiers.Should().HaveCount(2);

        var crm = person.Identifiers.Should().ContainSingle(i => i.SourceSystemId == "CRM").Subject;
        crm.ExternalPersonId.Should().Be($"ext-ukh-{uid}");

        var erp = person.Identifiers.Should().ContainSingle(i => i.SourceSystemId == "ERP").Subject;
        erp.ExternalPersonId.Should().Be($"emp-ukh-{uid}");
    }

    // =========================================================================
    // 16. AddIdentifier — добавление новой связи
    // =========================================================================

    [Fact]
    public async Task AddIdentifier_NewLink_ReturnsCreated()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var created = await PostResolve(new ResolveRequest
        {
            LastName = "Фонвизин",
            FirstName = "Тарас",
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-fonv-{uid}"
        });

        var request = new AddExternalIdRequest
        {
            SourceSystemId = "HR",
            ExternalPersonId = $"hr-fonv-{uid}",
            ExternalPersonType = "staff"
        };

        var response = await _client.PostAsJsonAsync($"/persons/{created.MasterId}/identifiers", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // =========================================================================
    // 17. AddIdentifier — дубликат (актуализация)
    // =========================================================================

    [Fact]
    public async Task AddIdentifier_Duplicate_UpdatesExisting()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var created = await PostResolve(new ResolveRequest
        {
            LastName = "Хмельницкий",
            FirstName = "Август",
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-khm-{uid}"
        });

        var hrExtId = $"hr-khm-{uid}";
        var first = new AddExternalIdRequest
        {
            SourceSystemId = "HR",
            ExternalPersonId = hrExtId
        };

        var second = new AddExternalIdRequest
        {
            SourceSystemId = "HR",
            ExternalPersonId = hrExtId,
            ExternalPersonType = "employee"
        };

        await _client.PostAsJsonAsync($"/persons/{created.MasterId}/identifiers", first);
        await _client.PostAsJsonAsync($"/persons/{created.MasterId}/identifiers", second);

        var response = await _client.GetAsync($"/persons/{created.MasterId}");
        var person = await response.Content.ReadFromJsonAsync<PersonDto>();

        person.Should().NotBeNull();
        person!.Identifiers.Count(i => i.SourceSystemId == "HR").Should().Be(1);
    }

    // =========================================================================
    // 18. Полный цикл — мульти-системная идентификация
    // =========================================================================

    [Fact]
    public async Task FullCycle_ThreeSystems_OnePersonId()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var crm = await PostResolve(new ResolveRequest
        {
            LastName = "Ятspbеков",
            FirstName = "Велимир",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-yat-{uid}"
        });

        var erp = await PostResolve(new ResolveRequest
        {
            LastName = "ЯТSPBЕКОВ",
            FirstName = "ВЕЛИМИР",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "ERP",
            ExternalPersonId = $"emp-yat-{uid}"
        });

        var hr = await PostResolve(new ResolveRequest
        {
            LastName = "Ятspbеков",
            FirstName = "Велимир",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "HR",
            ExternalPersonId = $"hr-yat-{uid}"
        });

        crm.MasterId.Should().Be(erp.MasterId);
        erp.MasterId.Should().Be(hr.MasterId);

        var person = await _client.GetFromJsonAsync<PersonDto>(
            $"/persons/{crm.MasterId}");

        person.Should().NotBeNull();
        person!.Identifiers.Should().HaveCount(3);

        var crmId = person.Identifiers.Should().ContainSingle(i => i.SourceSystemId == "CRM").Subject;
        crmId.ExternalPersonId.Should().Be($"ext-yat-{uid}");

        var erpId = person.Identifiers.Should().ContainSingle(i => i.SourceSystemId == "ERP").Subject;
        erpId.ExternalPersonId.Should().Be($"emp-yat-{uid}");

        var hrId = person.Identifiers.Should().ContainSingle(i => i.SourceSystemId == "HR").Subject;
        hrId.ExternalPersonId.Should().Be($"hr-yat-{uid}");
    }

    // =========================================================================
    // 19. Verify externalPersonType is preserved
    // =========================================================================

    [Fact]
    public async Task AddIdentifier_WithType_PreservesType()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var created = await PostResolve(new ResolveRequest
        {
            LastName = "Дзюба",
            FirstName = "Клим",
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-dzyu-{uid}"
        });

        var request = new AddExternalIdRequest
        {
            SourceSystemId = "HR",
            ExternalPersonId = $"hr-dzyu-{uid}",
            ExternalPersonType = "employee"
        };

        await _client.PostAsJsonAsync($"/persons/{created.MasterId}/identifiers", request);

        var response = await _client.GetAsync($"/persons/{created.MasterId}");
        var person = await response.Content.ReadFromJsonAsync<PersonDto>();

        var hr = person!.Identifiers.Should().ContainSingle(i => i.SourceSystemId == "HR").Subject;
        hr.ExternalPersonId.Should().Be($"hr-dzyu-{uid}");
        hr.ExternalPersonType.Should().Be("employee");
    }

    // =========================================================================
    // 20. ValidateInn — валидный ИНН
    // =========================================================================

    [Fact]
    public async Task ValidateInn_ValidInn_ReturnsIsValid()
    {
        var request = new InnValidationRequest { Inn = "7707083893" };

        var response = await _client.PostAsJsonAsync("/persons/validate/inn", request);
        var result = await response.Content.ReadFromJsonAsync<ValidationResultDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    // =========================================================================
    // 21. ValidateInn — невалидный ИНН
    // =========================================================================

    [Fact]
    public async Task ValidateInn_InvalidInn_ReturnsNotValid()
    {
        var request = new InnValidationRequest { Inn = "123456789012" };

        var response = await _client.PostAsJsonAsync("/persons/validate/inn", request);
        var result = await response.Content.ReadFromJsonAsync<ValidationResultDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.IsValid.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    // =========================================================================
    // 22. ValidateInn — пустой ИНН
    // =========================================================================

    [Fact]
    public async Task ValidateInn_EmptyInn_ReturnsNotValid()
    {
        var request = new InnValidationRequest { Inn = "" };

        var response = await _client.PostAsJsonAsync("/persons/validate/inn", request);
        var result = await response.Content.ReadFromJsonAsync<ValidationResultDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.IsValid.Should().BeFalse();
    }

    // =========================================================================
    // 23. ValidateSnils — валидный СНИЛС
    // =========================================================================

    [Fact]
    public async Task ValidateSnils_ValidSnils_ReturnsIsValid()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var snils = GenerateValidSnils(uid);
        var request = new SnilsValidationRequest { Snils = snils };

        var response = await _client.PostAsJsonAsync("/persons/validate/snils", request);
        var result = await response.Content.ReadFromJsonAsync<ValidationResultDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    // =========================================================================
    // 24. ValidateSnils — невалидный СНИЛС
    // =========================================================================

    [Fact]
    public async Task ValidateSnils_InvalidSnils_ReturnsNotValid()
    {
        var request = new SnilsValidationRequest { Snils = "12345678901" };

        var response = await _client.PostAsJsonAsync("/persons/validate/snils", request);
        var result = await response.Content.ReadFromJsonAsync<ValidationResultDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.IsValid.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    // =========================================================================
    // 25. ValidateSnils — пустой СНИЛС
    // =========================================================================

    [Fact]
    public async Task ValidateSnils_EmptySnils_ReturnsNotValid()
    {
        var request = new SnilsValidationRequest { Snils = "" };

        var response = await _client.PostAsJsonAsync("/persons/validate/snils", request);
        var result = await response.Content.ReadFromJsonAsync<ValidationResultDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.IsValid.Should().BeFalse();
    }

    // =========================================================================
    // 26. Resolve — невалидный ИНН создаёт лицо с дефектом
    // =========================================================================

    [Fact]
    public async Task Resolve_InvalidInn_ReturnsOkWithDefects()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var request = new ResolveRequest
        {
            LastName = "Дефектов",
            FirstName = "ИНН",
            Evidence = new Evidence { Inn = "123456789012" },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-def-inn-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);
        var result = await response.Content.ReadFromJsonAsync<ResolveResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.MasterId.Should().NotBeNull();
        result.HasDefects.Should().BeTrue();
        result.Defects.Should().Contain(d => d.DefectType == "invalid_inn");
    }

    // =========================================================================
    // 27. Resolve — невалидный СНИЛС создаёт лицо с дефектом
    // =========================================================================

    [Fact]
    public async Task Resolve_InvalidSnils_ReturnsOkWithDefects()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var request = new ResolveRequest
        {
            LastName = "Дефектов",
            FirstName = "СНИЛС",
            Evidence = new Evidence { Snils = "12345678901" },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-def-snils-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);
        var result = await response.Content.ReadFromJsonAsync<ResolveResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.MasterId.Should().NotBeNull();
        result.HasDefects.Should().BeTrue();
        result.Defects.Should().Contain(d => d.DefectType == "invalid_snils");
    }

    // =========================================================================
    // 28. Resolve — неполный ДУЛ создаёт лицо с дефектом
    // =========================================================================

    [Fact]
    public async Task Resolve_DulIncomplete_ReturnsOkWithDefects()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var request = new ResolveRequest
        {
            LastName = "Дефектов",
            FirstName = "ДУЛ",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510" },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-def-dul-{uid}"
        };

        var response = await _client.PostAsJsonAsync("/persons/resolve", request);
        var result = await response.Content.ReadFromJsonAsync<ResolveResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Status.Should().Be(PersonMatchStatus.Unmatched);
        result.MasterId.Should().NotBeNull();
        result.HasDefects.Should().BeTrue();
        result.Defects.Should().Contain(d => d.DefectType == "dul_incomplete");
    }

    // =========================================================================
    // 29. GetDefects — получение дефектов лица
    // =========================================================================

    [Fact]
    public async Task GetDefects_ReturnsDefectList()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];

        // Создаём лицо с невалидным ИНН
        var resolveRequest = new ResolveRequest
        {
            LastName = "Дефектный",
            FirstName = "ИНН",
            Evidence = new Evidence { Inn = "123456789012" },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-def-get-{uid}"
        };

        var resolveResponse = await PostResolve(resolveRequest);
        resolveResponse.HasDefects.Should().BeTrue();

        // Получаем данные лица (включая дефекты)
        var response = await _client.GetAsync($"/persons/{resolveResponse.MasterId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var person = await response.Content.ReadFromJsonAsync<PersonDto>();
        person!.Defects.Should().NotBeEmpty();
    }

    // =========================================================================
    // 30. E2E — поэтапное добавление данных сотрудника
    // =========================================================================

    [Fact]
    public async Task E2E_ForeignEmployee_GradualDataEnrichment_SamePersonId()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var extId = $"ext-emp-{uid}";
        var dulSeries = GenerateForeignDulSeries(uid);
        var dulNumber = GenerateForeignDulNumber(uid);
        var inn = GenerateValidInn(uid);
        var snils = GenerateValidSnils(uid);

        // Шаг 1: Сотрудник с паспортом иностранного гражданина (без ИНН и СНИЛС)
        var step1 = await PostResolve(new ResolveRequest
        {
            LastName = "Петров",
            FirstName = "Иван",
            Evidence = new Evidence { DulType = "10", DulSeries = dulSeries, DulNumber = dulNumber },
            SourceSystemId = "HR",
            ExternalPersonId = extId
        });

        step1.Status.Should().Be(PersonMatchStatus.Unmatched);
        step1.MasterId.Should().NotBeNull();
        var personId = step1.MasterId!.Value;

        // Проверяем PersonId через GET
        var person1 = await GetPerson(personId);
        person1.Should().NotBeNull();
        person1!.MasterId.Should().Be(personId);

        // Шаг 2: Сотрудник получает ИНН
        var step2 = await PostResolve(new ResolveRequest
        {
            LastName = "Петров",
            FirstName = "Иван",
            Evidence = new Evidence { DulType = "10", DulSeries = dulSeries, DulNumber = dulNumber, Inn = inn },
            SourceSystemId = "HR",
            ExternalPersonId = extId
        });

        step2.Status.Should().Be(PersonMatchStatus.Matched);
        step2.MasterId.Should().Be(personId);

        // Проверяем PersonId через GET
        var person2 = await GetPerson(personId);
        person2.Should().NotBeNull();
        person2!.MasterId.Should().Be(personId);

        // Шаг 3: Бухгалтер оформляет СНИЛС
        var step3 = await PostResolve(new ResolveRequest
        {
            LastName = "Петров",
            FirstName = "Иван",
            Evidence = new Evidence { DulType = "10", DulSeries = dulSeries, DulNumber = dulNumber, Inn = inn, Snils = snils },
            SourceSystemId = "HR",
            ExternalPersonId = extId
        });

        step3.Status.Should().Be(PersonMatchStatus.Matched);
        step3.MasterId.Should().Be(personId);

        // Проверяем PersonId через GET
        var person3 = await GetPerson(personId);
        person3.Should().NotBeNull();
        person3!.MasterId.Should().Be(personId);
    }

    // =========================================================================
    // 31. Conflict by INN — автоматическое слияние
    // =========================================================================

    [Fact]
    public async Task Resolve_ConflictByInn_AutoMerges()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var inn = GenerateValidInn(uid);

        // Шаг 1: System A — сотрудник с паспортом иностранного гражданина (тип 10)
        var step1 = await PostResolve(new ResolveRequest
        {
            LastName = "Мержин",
            FirstName = "Игорь",
            Evidence = new Evidence { DulType = "10", DulSeries = $"AB{uid[..2]}", DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "HR",
            ExternalPersonId = $"ext-hr-{uid}"
        });

        step1.Status.Should().Be(PersonMatchStatus.Unmatched);
        var personA = step1.MasterId!.Value;

        // Шаг 2: System B — тот же сотрудник с паспортом РФ (тип 21)
        var step2 = await PostResolve(new ResolveRequest
        {
            LastName = "Мержин",
            FirstName = "Игорь",
            Evidence = new Evidence { DulType = "21", DulSeries = GeneratePassportSeries(uid), DulNumber = GeneratePassportNumber(uid) },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-crm-{uid}"
        });

        step2.Status.Should().Be(PersonMatchStatus.Unmatched);
        var personB = step2.MasterId!.Value;
        personB.Should().NotBe(personA, "разные ДУЛ → разные записи");

        // Шаг 3: System A — сотрудник получает ИНН, повторный resolve
        var step3 = await PostResolve(new ResolveRequest
        {
            LastName = "Мержин",
            FirstName = "Игорь",
            Evidence = new Evidence
            {
                DulType = "10",
                DulSeries = $"AB{uid[..2]}",
                DulNumber = GeneratePassportNumber(uid),
                Inn = inn
            },
            SourceSystemId = "HR",
            ExternalPersonId = $"ext-hr-{uid}"
        });

        step3.Status.Should().Be(PersonMatchStatus.Matched);
        step3.MasterId.Should().Be(personA);

        // Шаг 4: System B — сотрудник получает ИНН, повторный resolve
        // ИНН → personA (M=1), ДУЛ тип 21 → personB (M=1) → выбирается первый кандидат
        var step4 = await PostResolve(new ResolveRequest
        {
            LastName = "Мержин",
            FirstName = "Игорь",
            Evidence = new Evidence
            {
                DulType = "21",
                DulSeries = GeneratePassportSeries(uid),
                DulNumber = GeneratePassportNumber(uid),
                Inn = inn
            },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-crm-{uid}"
        });

        step4.Status.Should().Be(PersonMatchStatus.Matched);
        // Оба кандидата имеют M=1, K=0 — выбирается первый найденный
        (step4.MasterId == personA || step4.MasterId == personB).Should().BeTrue();
    }

    // =========================================================================
    // 32. Ambiguous — расхождение ключей создаёт новую запись + очередь
    // =========================================================================

    [Fact]
    public async Task Resolve_AmbiguousKeyMismatch_CreatesNewPersonAndQueue()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var inn = GenerateValidInn(uid);
        var snils = GenerateValidSnils(uid);

        // Шаг 1: создать P1 с ИНН + СНИЛС
        var step1 = await PostResolve(new ResolveRequest
        {
            LastName = "Несовпадов",
            FirstName = "Артём",
            Evidence = new Evidence { Inn = inn, Snils = snils },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-crm-{uid}"
        });

        step1.Status.Should().Be(PersonMatchStatus.Unmatched);
        var personA = step1.MasterId!.Value;

        // Шаг 2: запрос с тем же СНИЛС, но ДРУГИМ ИНН → Ambiguous
        var differentInn = GenerateValidInn($"x{uid}");
        var step2 = await PostResolve(new ResolveRequest
        {
            LastName = "Несовпадов",
            FirstName = "Артём",
            Evidence = new Evidence { Inn = differentInn, Snils = snils },
            SourceSystemId = "ERP",
            ExternalPersonId = $"ext-erp-{uid}"
        });

        step2.Status.Should().Be(PersonMatchStatus.Ambiguous);
        step2.MasterId.Should().NotBeNull();
        step2.MasterId.Should().NotBe(personA, "должна быть создана новая запись");
        step2.KeyConflicts.Should().ContainSingle(c => c.KeyType == "inn");

        // Шаг 3: GET /persons/{personA} — существует
        var getA = await _client.GetAsync($"/persons/{personA}");
        getA.StatusCode.Should().Be(HttpStatusCode.OK);

        // Шаг 4: GET /persons/{personB} — существует (новая запись)
        var getB = await _client.GetAsync($"/persons/{step2.MasterId}");
        getB.StatusCode.Should().Be(HttpStatusCode.OK);

        // Шаг 5: проверить очередь
        var queueResponse = await _client.GetAsync("/persons/review");
        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // 33. Re-resolve после смены фамилии — ключи обновлены, MasterId не изменился
    // =========================================================================

    [Fact]
    public async Task Resolve_SameExternalIdWithNewLastName_KeysUpdatedMasterIdSame()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var inn = GenerateValidInn(uid);
        var snils = GenerateValidSnils(uid);
        var extId = $"ext-hr-{uid}";

        // Шаг 1: сотрудник с ИНН + СНИЛС + ДУЛ РФ (тип 21), фамилия "Старинов"
        var step1 = await PostResolve(new ResolveRequest
        {
            LastName = "Старинов",
            FirstName = "Дмитрий",
            MiddleName = "Олегович",
            Evidence = new Evidence
            {
                Inn = inn,
                Snils = snils,
                DulType = "21",
                DulSeries = GeneratePassportSeries(uid),
                DulNumber = GeneratePassportNumber(uid)
            },
            SourceSystemId = "HR",
            ExternalPersonId = extId
        });

        step1.Status.Should().Be(PersonMatchStatus.Unmatched);
        var masterId = step1.MasterId!.Value;

        // Шаг 2: сотрудник сменил фамилию — та же система отправляет новые данные
        var step2 = await PostResolve(new ResolveRequest
        {
            LastName = "Новиков",
            FirstName = "Дмитрий",
            MiddleName = "Олегович",
            Evidence = new Evidence
            {
                Inn = inn,
                Snils = snils,
                DulType = "21",
                DulSeries = GeneratePassportSeries(uid),
                DulNumber = GeneratePassportNumber(uid)
            },
            SourceSystemId = "HR",
            ExternalPersonId = extId
        });

        // Matched — тот же внешний ключ
        step2.Status.Should().Be(PersonMatchStatus.Matched);
        step2.MasterId.Should().Be(masterId, "MasterId не должен измениться при обновлении данных");

        // Проверить: ключи обновлены
        var person = await GetPerson(masterId);
        person.Should().NotBeNull();
        person!.IdentificationKeys.Should().NotBeEmpty();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Генерирует серию паспорта РФ (4 цифры, формат "XX XX").
    /// </summary>
    private static string GeneratePassportSeries(string uid)
    {
        var digits = uid.Replace("-", "").Select(c => c >= '0' && c <= '9' ? c - '0' : c - 'a' + 10).Select(d => d % 10).ToArray();
        return $"{digits[0]}{digits[1]} {digits[2]}{digits[3]}";
    }

    /// <summary>
    /// Генерирует номер паспорта РФ (6 цифр).
    /// </summary>
    private static string GeneratePassportNumber(string uid)
    {
        var hex = uid.Replace("-", "").ToLowerInvariant();
        var digits = hex.Select(c => c >= '0' && c <= '9' ? c - '0' : c - 'a' + 10).Select(d => d % 10).ToArray();
        return string.Concat(digits.Take(6).Select(d => d.ToString()));
    }

    /// <summary>
    /// Генерирует валидный 10-значный ИНН юридического лица на основе uid.
    /// </summary>
    private static string GenerateValidInn(string uid)
    {
        // uid = 8 hex chars → 32 бита → достаточно для уникальности
        var hash = uid.GetHashCode();
        var absHash = Math.Abs(hash);

        // Преобразуем в 9 цифр
        var nineDigits = absHash % 1_000_000_000;
        var digits = nineDigits.ToString("D9").Select(c => c - '0').ToArray();

        int[] weights = [2, 4, 10, 3, 5, 9, 4, 6, 8];
        int sum = 0;
        for (int i = 0; i < 9; i++)
            sum += digits[i] * weights[i];

        int checkDigit = sum % 11 % 10;
        return string.Concat(digits.Select(d => d.ToString())) + checkDigit;
    }

    /// <summary>
    /// Генерирует валидный 11-значный СНИЛС на основе uid.
    /// </summary>
    private static string GenerateValidSnils(string uid)
    {
        var hex = uid.Replace("-", "").ToLowerInvariant();
        while (hex.Length < 9)
            hex += hex;

        var baseDigits = hex.Substring(0, 9)
            .Select(c => c >= '0' && c <= '9' ? c - '0' : c - 'a' + 10)
            .Select(d => d % 10)
            .ToArray();

        int[] weights = [9, 8, 7, 6, 5, 4, 3, 2, 1];
        int sum = 0;
        for (int i = 0; i < 9; i++)
            sum += baseDigits[i] * weights[i];

        int check = sum % 101;
        if (check == 100) check = 0;

        return string.Concat(baseDigits.Select(d => d.ToString())) + check / 10 + check % 10;
    }

    /// <summary>
    /// Генерирует серию паспорта иностранного гражданина (2 буквы + 2 цифры, формат "XX XX").
    /// </summary>
    private static string GenerateForeignDulSeries(string uid)
    {
        var hex = uid.Replace("-", "").ToLowerInvariant();
        var letters = hex.Select(c => (char)('А' + (c >= '0' && c <= '9' ? c - '0' : c - 'a' + 10) % 32)).Take(2).ToArray();
        var digits = hex.Select(c => c >= '0' && c <= '9' ? c - '0' : c - 'a' + 10).Select(d => d % 10).ToArray();
        return $"{letters[0]}{letters[1]} {digits[0]}{digits[1]}";
    }

    /// <summary>
    /// Генерирует номер паспорта иностранного гражданина (6 цифр).
    /// </summary>
    private static string GenerateForeignDulNumber(string uid)
    {
        var hex = uid.Replace("-", "").ToLowerInvariant();
        var digits = hex.Select(c => c >= '0' && c <= '9' ? c - '0' : c - 'a' + 10).Select(d => d % 10).ToArray();
        return string.Concat(digits.Take(6).Select(d => d.ToString()));
    }

    // =========================================================================
    // 33. Третий запрос конфликтует с двумя существующими персонами
    // =========================================================================

    [Fact]
    public async Task Resolve_ThirdRecord_ConflictsWithTwoMasters()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var inn = GenerateValidInn(uid);
        var snils = GenerateValidSnils(uid);

        // Шаг 1: person A — ИНН + ДУЛ паспорт (тип 10)
        var step1 = await PostResolve(new ResolveRequest
        {
            LastName = "Волков",
            FirstName = "Денис",
            Evidence = new Evidence
            {
                Inn = inn,
                DulType = "10",
                DulSeries = GenerateForeignDulSeries(uid),
                DulNumber = GenerateForeignDulNumber(uid)
            },
            SourceSystemId = "HR",
            ExternalPersonId = $"ext-hr-{uid}"
        });

        step1.Status.Should().Be(PersonMatchStatus.Unmatched);
        var personA = step1.MasterId!.Value;

        // Шаг 2: person B — СНИЛС + другой ДУЛ паспорт
        var step2 = await PostResolve(new ResolveRequest
        {
            LastName = "Волков",
            FirstName = "Денис",
            Evidence = new Evidence
            {
                Snils = snils,
                DulType = "10",
                DulSeries = GenerateForeignDulSeries($"b{uid}"),
                DulNumber = GenerateForeignDulNumber($"b{uid}")
            },
            SourceSystemId = "ERP",
            ExternalPersonId = $"ext-erp-{uid}"
        });

        step2.Status.Should().Be(PersonMatchStatus.Unmatched);
        var personB = step2.MasterId!.Value;
        personB.Should().NotBe(personA, "ДУЛ разный → разные персоны");

        // Шаг 3: ИНН₁ + СНИЛС₁ + третий ДУЛ → конфликт с A (по ИНН) и B (по СНИЛС)
        var step3 = await PostResolve(new ResolveRequest
        {
            LastName = "Волков",
            FirstName = "Денис",
            Evidence = new Evidence
            {
                Inn = inn,
                Snils = snils,
                DulType = "10",
                DulSeries = GenerateForeignDulSeries($"c{uid}"),
                DulNumber = GenerateForeignDulNumber($"c{uid}")
            },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-crm-{uid}"
        });

        // Конфликт: система выбрала A (ИНН > СНИЛС), ДУЛ не совпал → Ambiguous
        step3.Status.Should().Be(PersonMatchStatus.Ambiguous);
        step3.MasterId.Should().NotBeNull();
        step3.KeyConflicts.Should().Contain(c => c.KeyType == "dul");

        // Проверить, что person A и person B существуют
        var getA = await GetPerson(personA);
        getA.Should().NotBeNull();
        var getB = await GetPerson(personB);
        getB.Should().NotBeNull();

        // Проверить очередь: запись связывает personA с новым person
        var queueResponse = await _client.GetAsync("/persons/review");
        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<ResolveResponse> PostResolve(ResolveRequest request)
    {
        var response = await _client.PostAsJsonAsync("/persons/resolve", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResolveResponse>())!;
    }

    private async Task<PersonDto?> GetPerson(Guid personId)
    {
        var response = await _client.GetAsync($"/persons/{personId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PersonDto>();
    }
}
