using LoadBalancer.Domain.Entities;

namespace LoadBalancer.Application.Interfaces;

/// <summary>
/// Probes a single backend node to determine whether it is healthy.
/// </summary>
public interface INodeHealthChecker
{
    /// <summary>
    /// Returns <c>true</c> when the node responds with a successful HTTP status code.
    /// </summary>
    Task<bool> CheckAsync(BackendNode node, CancellationToken cancellationToken = default);
}
