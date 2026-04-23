using LoadBalancer.Domain.Entities;
using LoadBalancer.Domain.Interfaces;

namespace LoadBalancer.Infrastructure.Strategies;

/// <summary>
/// Lightweight scope that decrements the node's active-request counter on dispose.
/// </summary>
internal sealed class RequestScope : IRequestScope
{
    private int _disposed;

    public BackendNode Node { get; }

    public RequestScope(BackendNode node)
    {
        Node = node;
        node.IncrementActive();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            Node.DecrementActive();
    }
}
