using Mnemonios.Domain.DTOs;

namespace Mnemonios.Domain.Interfaces;

/// <summary>
/// Сервис идентификации персон по предвычисленным хешам.
/// </summary>
public interface IPersonHashResolveService
{
    /// <summary>
    /// Идентифицирует персону по HMAC-SHA256 хешам — находит существующую или создаёт новую PersonID.
    /// </summary>
    Task<ResolveResponse> ResolveByHashesAsync(
        HashResolveRequest request,
        CancellationToken cancellationToken = default);
}
