using LoadBalancer.Application.Interfaces;
using LoadBalancer.Domain.Entities;

namespace LoadBalancer.Infrastructure.HealthChecks;

/// <summary>
/// Runs health checks on a fixed <see cref="HealthCheckOptions.Interval"/>.
/// <para>
/// Grace period: newly registered nodes are <b>not</b> probed until
/// <see cref="HealthCheckOptions.GracePeriod"/> has elapsed since
/// <see cref="BackendNode.RegisteredAt"/>. This prevents marking a
/// starting service unhealthy before it is ready.
/// </para>
/// Each probe uses a per-call timeout of <see cref="HealthCheckOptions.Timeout"/>.
/// </summary>
public sealed class PeriodicHealthCheckRunner : IHealthCheckRunner, IAsyncDisposable
{
    private readonly IReadOnlyList<BackendNode> _nodes;
    private readonly INodeHealthChecker _checker;
    private readonly HealthCheckOptions _options;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public PeriodicHealthCheckRunner(
        IReadOnlyList<BackendNode> nodes,
        INodeHealthChecker checker,
        HealthCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(options);

        _nodes   = nodes;
        _checker = checker;
        _options = options;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_cts is not null, this);

        _cts  = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null) return;

        await _cts.CancelAsync();

        if (_loop is not null)
        {
            try { await _loop.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { /* expected on shutdown */ }
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_options.Interval);

        while (await timer.WaitForNextTickAsync(ct))
        {
            // Fan-out: check all nodes concurrently within each tick.
            var tasks = _nodes.Select(node => CheckNodeAsync(node, ct));
            await Task.WhenAll(tasks);
        }
    }

    private async Task CheckNodeAsync(BackendNode node, CancellationToken ct)
    {
        // Honour the startup grace period — do not probe until the node has had time to start.
        var age = DateTimeOffset.UtcNow - node.RegisteredAt;
        if (age < _options.GracePeriod)
            return;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.Timeout);

        try
        {
            var healthy = await _checker.CheckAsync(node, timeoutCts.Token);

            if (healthy)
                node.MarkHealthy();
            else
                node.MarkUnhealthy();
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException)
        {
            // Timed out or parent cancelled — treat as unhealthy unless the parent loop is stopping.
            if (!ct.IsCancellationRequested)
                node.MarkUnhealthy();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
