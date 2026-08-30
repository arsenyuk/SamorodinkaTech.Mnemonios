using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Enums;
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
            SourceSystemId = "CRM",
            ExternalPersonId = extId1
        });

        await PostResolve(new ResolveRequest
        {
            LastName = "Лебедев",
            FirstName = "Дмитрий",
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
}
