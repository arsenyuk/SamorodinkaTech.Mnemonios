namespace Mnemonios.Domain.Interfaces;

/// <summary>
/// Провайдер IP-адреса текущего клиента.
/// Используется в декораторах аудита и логировании.
/// </summary>
public interface IClientIpProvider
{
    /// <summary>
    /// Возвращает IP-адрес текущего клиента.
    /// </summary>
    string GetClientIp();
}
