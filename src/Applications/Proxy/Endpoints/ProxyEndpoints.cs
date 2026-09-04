using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Interfaces;
using Mnemonios.Infrastructure.Services;
using Mnemonios.Proxy.Configuration;

namespace Mnemonios.Proxy.Endpoints;

/// <summary>
/// Эндпоинты proxy-сервиса: приём ПДн от источника, вычисление хешей, пересылка в основной API.
/// </summary>
public static class ProxyEndpoints
{
    private const string BaseTag = "Proxy";

    /// <summary>
    /// Регистрирует эндпоинты proxy-сервиса.
    /// </summary>
    public static IEndpointRouteBuilder MapProxyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/proxy")
            .WithTags(BaseTag);

        group.MapPost("/resolve", HandleResolveAsync)
            .WithName("ProxyResolve")
            .WithSummary("Идентификация персоны: вычисление хешей и пересылка в основной API.")
            .Produces<ResolveResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status502BadGateway);

        group.MapGet("/health", HandleHealthAsync)
            .WithName("ProxyHealth")
            .WithSummary("Проверка работоспособности proxy-сервиса.")
            .Produces(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> HandleResolveAsync(
        ResolveRequest request,
        IIdentificationKeyService keyService,
        IHttpClientFactory httpClientFactory,
        IOptions<ProxyConfig> config,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("ProxyEndpoints.Resolve");

        try
        {
            var computedKeys = keyService.ComputeKeys(request);

            var hashRequest = new HashResolveRequest
            {
                SourceSystemId = request.SourceSystemId,
                ExternalPersonId = request.ExternalPersonId,
                ExternalPersonType = request.ExternalPersonType,
                OrganizationUnitKey = request.OrganizationUnitKey,
                KeyInn = computedKeys.FirstOrDefault(k => k.KeyType == "inn")?.KeyValue,
                KeySnils = computedKeys.FirstOrDefault(k => k.KeyType == "snils")?.KeyValue,
                KeyDul = computedKeys.FirstOrDefault(k => k.KeyType == "dul")?.KeyValue,
                KeyInnFio = computedKeys.FirstOrDefault(k => k.KeyType == "inn_fio")?.KeyValue,
                KeySnilsFio = computedKeys.FirstOrDefault(k => k.KeyType == "snils_fio")?.KeyValue,
                KeyDulFio = computedKeys.FirstOrDefault(k => k.KeyType == "dul_fio")?.KeyValue
            };

            var client = httpClientFactory.CreateClient("MnemoniosApi");
            var apiUrl = $"{config.Value.MnemoniosApiUrl.TrimEnd('/')}/persons/resolve-by-hashes";

            logger.LogInformation(
                "Отправка хешей в основной API: url={Url}, sourceSystem={SourceSystem}, externalId={ExternalId}",
                apiUrl, request.SourceSystemId, request.ExternalPersonId);

            var response = await client.PostAsJsonAsync(apiUrl, hashRequest, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogError(
                    "Ошибка основного API: url={Url}, statusCode={StatusCode}, body={Body}",
                    apiUrl, response.StatusCode, errorBody);

                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }

            var result = await response.Content.ReadFromJsonAsync<ResolveResponse>(ct);
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка proxy-сервиса при обработке запроса");
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static IResult HandleHealthAsync()
    {
        return Results.Ok(new { status = "healthy", service = "proxy" });
    }
}
