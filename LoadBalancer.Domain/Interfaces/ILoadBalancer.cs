using LoadBalancer.Domain.Entities;

namespace LoadBalancer.Domain.Interfaces;

/// <summary>
/// Core load-balancing abstraction. Implementations decide which backend
/// node should handle the next request.
/// </summary>
public interface ILoadBalancer
{
    /// <summary>
    /// Returns the next backend node to handle a request, or <c>null</c>
    /// when no healthy nodes are available.
    /// </summary>
    BackendNode? Next();

    /// <summary>
    /// Selects the next backend node and returns a scope handle that
    /// decrements the node's active-request counter when disposed.
    /// Use with <c>await using</c> or <c>using</c> to track in-flight requests.
    /// Returns <c>null</c> when no healthy nodes are available.
    /// </summary>
    IRequestScope? Track();
}
