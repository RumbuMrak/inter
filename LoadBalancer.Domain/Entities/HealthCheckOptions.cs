namespace LoadBalancer.Domain.Entities;

/// <summary>
/// Configuration for the periodic health-check mechanism.
/// </summary>
public sealed class HealthCheckOptions
{
    /// <summary>
    /// How long after a node is registered before health checks begin.
    /// This gives a freshly-started node time to become ready.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan GracePeriod { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How often each node is probed.
    /// Default: 15 seconds.
    /// </summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Maximum time to wait for a single health-check HTTP call before timing out.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Path appended to the node address when probing.
    /// Default: "/health".
    /// </summary>
    public string HealthPath { get; init; } = "/health";
}
