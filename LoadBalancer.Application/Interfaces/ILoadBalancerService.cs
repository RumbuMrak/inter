using LoadBalancer.Application.DTOs;

namespace LoadBalancer.Application.Interfaces;

/// <summary>
/// Application-level service that orchestrates backend selection.
/// </summary>
public interface ILoadBalancerService
{
    /// <summary>Selects the next backend node for a request.</summary>
    SelectBackendResponse SelectBackend(SelectBackendRequest request);
}
