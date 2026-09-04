namespace Mnemonios.Proxy.Configuration;

/// <summary>
/// Конфигурация proxy-сервиса.
/// </summary>
public class ProxyConfig
{
    /// <summary>URL основного API для отправки вычисленных хешей.</summary>
    public string MnemoniosApiUrl { get; set; } = string.Empty;

    /// <summary>Таймаут HTTP-запроса к основному API (в секундах).</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
