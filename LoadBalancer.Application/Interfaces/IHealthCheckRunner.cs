namespace LoadBalancer.Application.Interfaces;

/// <summary>
/// Manages the lifecycle of the background health-check loop.
/// </summary>
public interface IHealthCheckRunner
{
    /// <summary>Begins periodic health checks for all registered nodes.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the health-check loop gracefully.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
