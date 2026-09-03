using System.Net;
using Microsoft.AspNetCore.Http;

namespace Mnemonios.Infrastructure;

/// <summary>
/// Извлечение реального IP-адреса клиента из HTTP-запроса.
/// Приоритет: X-Forwarded-For (первый IP) → RemoteIpAddress.
/// X-Forwarded-For валидируется на корректность формата IP.
/// </summary>
public static class ClientIpHelper
{
    private const string FallbackIp = "unknown";

    /// <summary>
    /// Извлекает реальный IP клиента из HTTP-запроса.
    /// </summary>
    /// <param name="http">HTTP-контекст.</param>
    /// <returns>IP-адрес клиента или "unknown".</returns>
    public static string GetClientIp(HttpContext http)
    {
        var forwardedFor = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstIp = forwardedFor
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim();

            if (!string.IsNullOrWhiteSpace(firstIp) && IsValidIp(firstIp))
                return firstIp;
        }

        return http.Connection.RemoteIpAddress?.ToString() ?? FallbackIp;
    }

    private static bool IsValidIp(string ip)
    {
        return IPAddress.TryParse(ip, out _);
    }
}
