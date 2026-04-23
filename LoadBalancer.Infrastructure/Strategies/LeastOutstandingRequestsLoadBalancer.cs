using LoadBalancer.Domain.Entities;
using LoadBalancer.Domain.Interfaces;

namespace LoadBalancer.Infrastructure.Strategies;

/// <summary>
/// Least-Outstanding-Requests (LOR) load balancer.
/// Each call selects the healthy node with the fewest active in-flight requests,
/// breaking ties by original registration order (stable, predictable).
/// <para>
/// Thread-safe: node selection and counter increment are performed atomically
/// inside a lock so no request is lost between pick and increment.
/// Use <see cref="Track"/> (preferred) to automatically decrement the counter
/// when the request completes, or manage <see cref="BackendNode.ActiveRequests"/>
/// manually via <see cref="ILoadBalancer.Next"/>.
/// </para>
/// </summary>
public sealed class LeastOutstandingRequestsLoadBalancer : ILoadBalancer
{
    private readonly IReadOnlyList<BackendNode> _nodes;
    private readonly object _lock = new();

    public LeastOutstandingRequestsLoadBalancer(IReadOnlyList<BackendNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        _nodes = nodes;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Does NOT increment <see cref="BackendNode.ActiveRequests"/>.
    /// Prefer <see cref="Track"/> so in-flight counts stay accurate.
    /// </remarks>
    public BackendNode? Next()
    {
        lock (_lock)
        {
            return PickHealthiest();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Atomically picks the least-loaded node and increments its counter.
    /// Dispose the returned scope when the request finishes to decrement.
    /// </remarks>
    public IRequestScope? Track()
    {
        lock (_lock)
        {
            var node = PickHealthiest();
            return node is null ? null : new RequestScope(node);
        }
    }

    // Must be called under _lock.
    private BackendNode? PickHealthiest()
    {
        BackendNode? best = null;

        foreach (var node in _nodes)
        {
            if (!node.IsHealthy) continue;

            if (best is null || node.ActiveRequests < best.ActiveRequests)
                best = node;
        }

        return best;
    }
}
