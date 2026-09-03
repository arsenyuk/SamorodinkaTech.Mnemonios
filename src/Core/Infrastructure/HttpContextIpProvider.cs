using Microsoft.AspNetCore.Http;
using Mnemonios.Domain.Interfaces;

namespace Mnemonios.Infrastructure;

/// <summary>
/// Реализация IClientIpProvider через IHttpContextAccessor.
/// </summary>
public class HttpContextIpProvider : IClientIpProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextIpProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public string GetClientIp()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
            return "internal";

        return ClientIpHelper.GetClientIp(context);
    }
}
