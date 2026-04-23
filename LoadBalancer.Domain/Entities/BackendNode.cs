namespace LoadBalancer.Domain.Entities;

/// <summary>
/// Represents a single backend service instance that can receive requests.
/// </summary>
public sealed class BackendNode
{
    public string Id { get; }
    public string Address { get; }
    public bool IsHealthy { get; private set; }

    /// <summary>UTC timestamp of when this node was registered. Used to enforce the startup grace period.</summary>
    public DateTimeOffset RegisteredAt { get; }

    /// <summary>
    /// Number of requests currently in-flight to this node.
    /// Incremented when a request is dispatched and decremented when it completes.
    /// Thread-safe via <see cref="Interlocked"/>.
    /// </summary>
    public int ActiveRequests => _activeRequests;
    private int _activeRequests;

    public BackendNode(string id, string address, DateTimeOffset? registeredAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        Id = id;
        Address = address;
        IsHealthy = true;
        RegisteredAt = registeredAt ?? DateTimeOffset.UtcNow;
    }

    public void MarkHealthy() => IsHealthy = true;
    public void MarkUnhealthy() => IsHealthy = false;

    public void IncrementActive() => Interlocked.Increment(ref _activeRequests);
    public void DecrementActive() => Interlocked.Decrement(ref _activeRequests);
}
