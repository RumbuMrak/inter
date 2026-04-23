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
}
