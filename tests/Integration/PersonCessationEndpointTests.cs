using System.Data;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Enums;
using Npgsql;
using Xunit;

namespace Mnemonios.IntegrationTests;

/// <summary>
/// E2E тесты для прекращения обработки персональных данных.
/// Требуют реальную PostgreSQL (через TestWebApplicationFactory).
/// </summary>
public class PersonCessationEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PersonCessationEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // =========================================================================
    // 1. Cessation — прекращение обработки существующей персоны
    // =========================================================================

    [Fact]
    public async Task Cessation_ExistingPerson_DeletesAndReturns200()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var extId = $"ext-cess-{uid}";

        var resolved = await PostResolve(new ResolveRequest
        {
            LastName = "Казаков",
            FirstName = "Пётр",
            SourceSystemId = "CRM",
            ExternalPersonId = extId
        });

        resolved.Status.Should().Be(PersonMatchStatus.Unmatched);
        var personId = resolved.MasterId!.Value;

        // Фаза 1: пометка
        var response = await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest
        {
            Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = extId }]
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CessationResponse>();
        result.Should().NotBeNull();
        result!.MasterId.Should().Be(personId);

        // Фаза 2: реконсилизация
        await Reconcile();

        // Персона удалена
        var getResponse = await _client.GetAsync($"/persons/{personId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================================================
    // 2. Cessation — после отзыва и реконсилизации персона не находится
    // =========================================================================

    [Fact]
    public async Task Cessation_AfterCessation_PersonNotFound()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var extId = $"ext-cess-gone-{uid}";

        await PostResolve(new ResolveRequest
        {
            LastName = "Зайцев",
            FirstName = "Аркадий",
            SourceSystemId = "CRM",
            ExternalPersonId = extId
        });

        // Фаза 1: пометка
        var first = await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest { Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = extId }] });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Фаза 2: реконсилизация
        await Reconcile();

        // Второй cessation — лицо уже удалено → 404
        var second = await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest { Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = extId }] });
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================================================
    // 3. Cessation — несуществующая связь возвращает 404
    // =========================================================================

    [Fact]
    public async Task Cessation_NonexistentExternalId_Returns404()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];

        var response = await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest { Identifiers = [new CessationIdentifierDto { SourceSystemId = "NONEXISTENT", ExternalPersonId = $"ext-ghost-{uid}" }] });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================================================
    // 4. Cessation — после отзыва и реконсилизации GET /persons/{id} не находит персону
    // =========================================================================

    [Fact]
    public async Task Cessation_AfterCessation_GetPersonReturns404()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var extId = $"ext-cess-get-{uid}";

        var resolved = await PostResolve(new ResolveRequest
        {
            LastName = "Белов",
            FirstName = "Сергей",
            SourceSystemId = "ERP",
            ExternalPersonId = extId
        });

        var personId = resolved.MasterId!.Value;

        // Фаза 1: пометка
        await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest { Identifiers = [new CessationIdentifierDto { SourceSystemId = "ERP", ExternalPersonId = extId }] });

        // Фаза 2: реконсилизация
        await Reconcile();

        var getResponse = await _client.GetAsync($"/persons/{personId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================================================
    // 5. Cessation — после отзыва и реконсилизации resolve создаёт нового
    // =========================================================================

    [Fact]
    public async Task Cessation_AfterCessation_SameDataCreatesNewPerson()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var extId = $"ext-cess-reborn-{uid}";

        var first = await PostResolve(new ResolveRequest
        {
            LastName = "Возрождённый",
            FirstName = "Алексей",
            SourceSystemId = "CRM",
            ExternalPersonId = extId
        });

        // Фаза 1: пометка
        await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest { Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = extId }] });

        // Фаза 2: реконсилизация
        await Reconcile();

        var second = await PostResolve(new ResolveRequest
        {
            LastName = "Возрождённый",
            FirstName = "Алексей",
            SourceSystemId = "CRM",
            ExternalPersonId = extId
        });

        second.Status.Should().Be(PersonMatchStatus.Unmatched);
        second.MasterId!.Value.Should().NotBe(first.MasterId!.Value);
    }

    // =========================================================================
    // 6. Cessation — с дефектами
    // =========================================================================

    [Fact]
    public async Task Cessation_PersonWithDefects_DeletesDefects()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var extId = $"ext-cess-def-{uid}";

        var resolved = await PostResolve(new ResolveRequest
        {
            LastName = "Соколов",
            FirstName = "Алексей",
            Evidence = new Evidence { Inn = "123456789012" },
            SourceSystemId = "CRM",
            ExternalPersonId = extId
        });

        resolved.HasDefects.Should().BeTrue();

        // Фаза 1: пометка
        var response = await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest
        {
            Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = extId }]
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Фаза 2: реконсилизация
        await Reconcile();

        // Персона удалена
        var getResponse = await _client.GetAsync($"/persons/{resolved.MasterId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================================================
    // 7. Cessation — person с несколькими внешними ссылками
    // =========================================================================

    [Fact]
    public async Task Cessation_PersonWithMultipleExternalIds_OnlySpecificLinkDeleted()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var extId1 = $"ext-cess-multi1-{uid}";
        var extId2 = $"ext-cess-multi2-{uid}";

        var resolved = await PostResolve(new ResolveRequest
        {
            LastName = "Лебедев",
            FirstName = "Дмитрий",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = $"{uid[..6]}" },
            SourceSystemId = "CRM",
            ExternalPersonId = extId1
        });

        await PostResolve(new ResolveRequest
        {
            LastName = "Лебедев",
            FirstName = "Дмитрий",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = $"{uid[..6]}" },
            SourceSystemId = "ERP",
            ExternalPersonId = extId2
        });

        // Фаза 1: пометка только CRM ссылки
        var response = await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest { Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = extId1 }] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Фаза 2: реконсилизация
        await Reconcile();

        // Персона остаётся — есть ещё связь с ERP
        var getResponse = await _client.GetAsync($"/persons/{resolved.MasterId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // 8. Cessation — режим org-unit-key: организация не найдена → 404
    // =========================================================================

    [Fact]
    public async Task Cessation_EmptyIdentifiers_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest
        {
            OrganizationUnitKey = "ORG-001"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================================================
    // 9. Cessation — сотрудник в двух организациях, cessation только в одной
    // =========================================================================

    [Fact]
    public async Task Cessation_PersonInTwoOrgs_OneOrgCessation_MasterStillExists()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var extId1 = $"ext-crm-{uid}";
        var extId2 = $"ext-erp-{uid}";

        // Сотрудник добавлен в CRM (организация Org-A)
        var crm = await PostResolve(new ResolveRequest
        {
            LastName = "Двухоргов",
            FirstName = "Андрей",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = $"{uid[..6]}" },
            SourceSystemId = "CRM",
            ExternalPersonId = extId1,
            OrganizationUnitKey = "Org-A"
        });

        crm.Status.Should().Be(PersonMatchStatus.Unmatched);
        var personId = crm.MasterId!.Value;

        // Сотрудник добавлен в ERP (организация Org-B) — тот же человек
        var erp = await PostResolve(new ResolveRequest
        {
            LastName = "Двухоргов",
            FirstName = "Андрей",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = $"{uid[..6]}" },
            SourceSystemId = "ERP",
            ExternalPersonId = extId2,
            OrganizationUnitKey = "Org-B"
        });

        erp.Status.Should().Be(PersonMatchStatus.Matched);
        erp.MasterId.Should().Be(personId);

        // Org-A прекращает обработку (по идентификатору CRM)
        var cease = await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest
        {
            Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = extId1 }]
        });
        cease.StatusCode.Should().Be(HttpStatusCode.OK);

        // Реконсилизация
        await Reconcile();

        // Мастер-запись сохраняется — Org-B ещё ссылается
        var getResponse = await _client.GetAsync($"/persons/{personId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var person = await getResponse.Content.ReadFromJsonAsync<PersonDto>();
        person.Should().NotBeNull();
        person!.Identifiers.Should().ContainSingle(i => i.SourceSystemId == "ERP");
    }

    // =========================================================================
    // 10. Cessation — сотрудник в двух организациях, cessation в обеих
    // =========================================================================

    [Fact]
    public async Task Cessation_PersonInTwoOrgs_BothOrgsCessation_MasterDeleted()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var extId1 = $"ext-crm-{uid}";
        var extId2 = $"ext-erp-{uid}";

        // Сотрудник добавлен в CRM (организация Org-A)
        var crm = await PostResolve(new ResolveRequest
        {
            LastName = "ДвухорговУдалённый",
            FirstName = "Борис",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = $"{uid[..6]}" },
            SourceSystemId = "CRM",
            ExternalPersonId = extId1,
            OrganizationUnitKey = "Org-A"
        });

        var personId = crm.MasterId!.Value;

        // Сотрудник добавлен в ERP (организация Org-B)
        var erp = await PostResolve(new ResolveRequest
        {
            LastName = "ДвухорговУдалённый",
            FirstName = "Борис",
            Evidence = new Evidence { DulType = "21", DulSeries = "4510", DulNumber = $"{uid[..6]}" },
            SourceSystemId = "ERP",
            ExternalPersonId = extId2,
            OrganizationUnitKey = "Org-B"
        });

        erp.MasterId.Should().Be(personId);

        // Org-A прекращает обработку
        await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest
        {
            Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = extId1 }]
        });

        // Реконсилизация — ссылка Org-A удалена, но Org-B остаётся
        await Reconcile();

        var afterFirst = await _client.GetAsync($"/persons/{personId}");
        afterFirst.StatusCode.Should().Be(HttpStatusCode.OK);

        // Org-B прекращает обработку
        await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest
        {
            Identifiers = [new CessationIdentifierDto { SourceSystemId = "ERP", ExternalPersonId = extId2 }]
        });

        // Реконсилизация — ссылок нет → мастер-запись удалена
        await Reconcile();

        var afterSecond = await _client.GetAsync($"/persons/{personId}");
        afterSecond.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================================================
    // 11. Deferred cessation — запись НЕ удаляется до наступления срока
    // =========================================================================

    [Fact]
    public async Task DeferredCessation_BeforeScheduledDate_PersonStillExists()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var extId = $"ext-defer-{uid}";

        var resolved = await PostResolve(new ResolveRequest
        {
            LastName = "Отложенный",
            FirstName = "Игорь",
            SourceSystemId = "CRM",
            ExternalPersonId = extId
        });

        var personId = resolved.MasterId!.Value;

        // Запланировать прекращение через 30 дней от текущего момента
        var scheduledDate = DateTime.UtcNow.AddDays(30);

        var deferResponse = await _client.PostAsJsonAsync("/persons/cessation/deferred", new DeferredCessationRequest
        {
            Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = extId }],
            ScheduledDeletionDate = scheduledDate,
            OrganizationUnitKey = ""
        });

        deferResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deferResult = await deferResponse.Content.ReadFromJsonAsync<DeferredCessationResponse>();
        deferResult.Should().NotBeNull();
        deferResult!.MasterId.Should().Be(personId);
        deferResult.ScheduledDeletionDate.Should().BeCloseTo(scheduledDate, TimeSpan.FromSeconds(5));

        // Реконсилизация не удаляет запись — срок не наступил
        await Reconcile();

        // Персона всё ещё существует
        var getResponse = await _client.GetAsync($"/persons/{personId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var person = await getResponse.Content.ReadFromJsonAsync<PersonDto>();
        person.Should().NotBeNull();
        person!.MasterId.Should().Be(personId);
    }

    // =========================================================================
    // 12. Cessation — два ДУЛ (паспорт РФ + паспорт иностранца), реконсилизация
    // =========================================================================

    [Fact]
    public async Task Cessation_MultipleDulSystems_PreservesDocuments()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var snils = GenerateValidSnils(uid);

        // Система HR: паспорт иностранного гражданина (тип 10)
        var hr = await PostResolve(new ResolveRequest
        {
            LastName = "Двуликов",
            FirstName = "Антон",
            MiddleName = "Сергеевич",
            Evidence = new Evidence
            {
                DulType = "10",
                DulSeries = $"AB{uid[..2]}",
                DulNumber = $"{uid[..6]}",
                Snils = snils
            },
            SourceSystemId = "HR",
            ExternalPersonId = $"ext-hr-{uid}"
        });

        hr.Status.Should().Be(PersonMatchStatus.Unmatched);
        var personId = hr.MasterId!.Value;

        // Система CRM: паспорт гражданина РФ (тип 21), тот же ФИО + СНИЛС
        var crm = await PostResolve(new ResolveRequest
        {
            LastName = "Двуликов",
            FirstName = "Антон",
            MiddleName = "Сергеевич",
            Evidence = new Evidence
            {
                DulType = "21",
                DulSeries = $"{uid[..4]}",
                DulNumber = $"{uid[..6]}",
                Snils = snils
            },
            SourceSystemId = "CRM",
            ExternalPersonId = $"ext-crm-{uid}"
        });

        crm.Status.Should().Be(PersonMatchStatus.Matched);
        crm.MasterId.Should().Be(personId);

        // Проверить: в person_documents 2 записи (тип 10 и тип 21)
        var docsAfterResolve = await GetPersonDocuments(personId);
        docsAfterResolve.Should().HaveCount(2);
        docsAfterResolve.Should().Contain(d => d.DocumentType == "10");
        docsAfterResolve.Should().Contain(d => d.DocumentType == "21");

        // HR прекращает обработку
        var ceaseHr = await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest
        {
            Identifiers = [new CessationIdentifierDto { SourceSystemId = "HR", ExternalPersonId = $"ext-hr-{uid}" }]
        });
        ceaseHr.StatusCode.Should().Be(HttpStatusCode.OK);

        // Реконсилизация — ссылка HR удалена, но CRM остаётся
        await Reconcile();

        // Персона всё ещё существует
        var getAfterHr = await _client.GetAsync($"/persons/{personId}");
        getAfterHr.StatusCode.Should().Be(HttpStatusCode.OK);

        // Оба ДУЛ сохранены (ДУЛ — свойство золотой записи, не внешней ссылки)
        var docsAfterHrCessation = await GetPersonDocuments(personId);
        docsAfterHrCessation.Should().HaveCount(2);

        // CRM прекращает обработку
        var ceaseCrm = await _client.PostAsJsonAsync("/persons/cessation", new CessationRequest
        {
            Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = $"ext-crm-{uid}" }]
        });
        ceaseCrm.StatusCode.Should().Be(HttpStatusCode.OK);

        // Реконсилизация — ссылок нет → персона удалена
        await Reconcile();

        var getAfterAll = await _client.GetAsync($"/persons/{personId}");
        getAfterAll.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Документы тоже удалены
        var docsAfterFullDeletion = await CountPersonDocuments(personId);
        docsAfterFullDeletion.Should().Be(0);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<ResolveResponse> PostResolve(ResolveRequest request)
    {
        var response = await _client.PostAsJsonAsync("/persons/resolve", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResolveResponse>())!;
    }

    private async Task Reconcile()
    {
        var response = await _client.PostAsJsonAsync("/persons/cessation/reconcile", new { });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<int> CountPersonDocuments(Guid masterId)
    {
        await using var connection = new NpgsqlConnection("Host=localhost;Port=5432;Database=mnemonios;Username=mnemonios;Password=mnemonios_dev");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM person_documents WHERE person_id = @pid";
        cmd.Parameters.Add(new NpgsqlParameter("pid", masterId));

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task<List<(string DocumentType, string DocumentHash)>> GetPersonDocuments(Guid masterId)
    {
        await using var connection = new NpgsqlConnection("Host=localhost;Port=5432;Database=mnemonios;Username=mnemonios;Password=mnemonios_dev");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT document_type, document_hash FROM person_documents WHERE person_id = @pid ORDER BY document_type";
        cmd.Parameters.Add(new NpgsqlParameter("pid", masterId));

        var docs = new List<(string, string)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            docs.Add((reader.GetString(0), reader.GetString(1)));
        }
        return docs;
    }

    /// <summary>
    /// Генерирует валидный 11-значный СНИЛС на основе uid.
    /// </summary>
    private static string GenerateValidSnils(string uid)
    {
        var hash = uid.GetHashCode();
        var absHash = Math.Abs(hash);
        var nineDigits = absHash % 1_000_000_000;
        var baseDigits = nineDigits.ToString("D9").Select(c => c - '0').ToArray();

        int[] weights1 = [9, 8, 7, 6, 5, 4, 3, 2, 1];
        int sum1 = 0;
        for (int i = 0; i < 9; i++)
            sum1 += baseDigits[i] * weights1[i];

        int check1 = sum1 % 101;
        if (check1 == 100) check1 = 0;

        int digit10 = check1 / 10;
        int digit11 = check1 % 10;

        return string.Concat(baseDigits.Select(d => d.ToString())) + digit10 + digit11;
    }
}
