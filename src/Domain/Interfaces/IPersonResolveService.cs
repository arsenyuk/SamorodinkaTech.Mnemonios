using Mnemonios.Domain.DTOs;

namespace Mnemonios.Domain.Interfaces;

/// <summary>
/// Сервис идентификации персон в MPI.
/// </summary>
public interface IPersonResolveService
{
    /// <summary>
    /// Resolves a person record — finds existing or creates new PersonID.
    /// </summary>
    Task<ResolveResponse> ResolveAsync(
        ResolveRequest request,
        CancellationToken cancellationToken = default);
}
