using LoadBalancer.Domain.Entities;
using LoadBalancer.Domain.Interfaces;

namespace LoadBalancer.Infrastructure.Strategies;

/// <summary>
/// Round-robin load balancer. Cycles through healthy backend nodes in order.
/// Thread-safe via <see cref="Interlocked"/>.
/// </summary>
public sealed class RoundRobinLoadBalancer : ILoadBalancer
{
    private readonly IReadOnlyList<BackendNode> _nodes;
    private int _counter = -1;

    /// <param name="nodes">Ordered list of backend nodes to balance across.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="nodes"/> is empty.</exception>
    public RoundRobinLoadBalancer(IReadOnlyList<BackendNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        _nodes = nodes;
    }

    /// <inheritdoc/>
    public BackendNode? Next()
    {
        var healthy = _nodes.Where(n => n.IsHealthy).ToList();

        if (healthy.Count == 0)
            return null;

        // Increment atomically and wrap around the healthy list size.
        var index = (int)((uint)Interlocked.Increment(ref _counter) % (uint)healthy.Count);
        return healthy[index];
    }

    /// <inheritdoc/>
    public IRequestScope? Track()
    {
        var node = Next();
        return node is null ? null : new RequestScope(node);
    }
}
