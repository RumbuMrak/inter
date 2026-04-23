using LoadBalancer.Application.DTOs;
using LoadBalancer.Application.Interfaces;
using LoadBalancer.Domain.Interfaces;

namespace LoadBalancer.Application.Services;

/// <summary>
/// Orchestrates backend selection by delegating to an <see cref="ILoadBalancer"/>
/// strategy and mapping domain objects to DTOs.
/// </summary>
public sealed class LoadBalancerService : ILoadBalancerService
{
    private readonly ILoadBalancer _loadBalancer;

    public LoadBalancerService(ILoadBalancer loadBalancer)
    {
        ArgumentNullException.ThrowIfNull(loadBalancer);
        _loadBalancer = loadBalancer;
    }

    public SelectBackendResponse SelectBackend(SelectBackendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var node = _loadBalancer.Next();

        if (node is null)
            return new SelectBackendResponse(false, null, "No healthy backend nodes available.");

        return new SelectBackendResponse(true, new BackendNodeDto(node.Id, node.Address), null);
    }
}
