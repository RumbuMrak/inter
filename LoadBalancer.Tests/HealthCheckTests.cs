using LoadBalancer.Application.Interfaces;
using LoadBalancer.Domain.Entities;
using LoadBalancer.Infrastructure.HealthChecks;

namespace LoadBalancer.Tests;

// ---------------------------------------------------------------------------
// Fake INodeHealthChecker for deterministic test control
// ---------------------------------------------------------------------------

file sealed class FakeHealthChecker : INodeHealthChecker
{
    private readonly Dictionary<string, bool> _results;

    public FakeHealthChecker(Dictionary<string, bool> results)
        => _results = results;

    public int CallCount { get; private set; }

    public Task<bool> CheckAsync(BackendNode node, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(_results.TryGetValue(node.Id, out var r) && r);
    }
}

// ---------------------------------------------------------------------------
// Grace period tests
// ---------------------------------------------------------------------------

public sealed class PeriodicHealthCheckRunnerGracePeriodTests
{
    [Fact]
    public async Task NodeInGracePeriod_IsNotProbed()
    {
        var node    = new BackendNode("n1", "http://a", registeredAt: DateTimeOffset.UtcNow);
        var checker = new FakeHealthChecker(new() { ["n1"] = true });
        var options = new HealthCheckOptions
        {
            GracePeriod = TimeSpan.FromHours(1),   // far in the future
            Interval    = TimeSpan.FromMilliseconds(50),
            Timeout     = TimeSpan.FromSeconds(1),
        };

        await using var runner = new PeriodicHealthCheckRunner([node], checker, options);
        await runner.StartAsync();

        // Allow two ticks to fire
        await Task.Delay(200);
        await runner.StopAsync();

        Assert.Equal(0, checker.CallCount);
        Assert.True(node.IsHealthy); // stays healthy (initial state)
    }

    [Fact]
    public async Task NodePastGracePeriod_IsProbed()
    {
        // Simulate a node that was registered 1 minute ago — past any reasonable grace period
        var node = new BackendNode("n1", "http://a",
            registeredAt: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1));

        var checker = new FakeHealthChecker(new() { ["n1"] = true });
        var options = new HealthCheckOptions
        {
            GracePeriod = TimeSpan.FromSeconds(5),
            Interval    = TimeSpan.FromMilliseconds(50),
            Timeout     = TimeSpan.FromSeconds(1),
        };

        await using var runner = new PeriodicHealthCheckRunner([node], checker, options);
        await runner.StartAsync();

        await Task.Delay(200); // allow at least one tick
        await runner.StopAsync();

        Assert.True(checker.CallCount > 0);
    }
}

// ---------------------------------------------------------------------------
// Health state transition tests
// ---------------------------------------------------------------------------

public sealed class PeriodicHealthCheckRunnerHealthStateTests
{
    private static HealthCheckOptions FastOptions() => new()
    {
        GracePeriod = TimeSpan.Zero,
        Interval    = TimeSpan.FromMilliseconds(50),
        Timeout     = TimeSpan.FromSeconds(1),
    };

    [Fact]
    public async Task HealthyResponse_MarksNodeHealthy()
    {
        var node    = new BackendNode("n1", "http://a", registeredAt: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1));
        var checker = new FakeHealthChecker(new() { ["n1"] = true });

        await using var runner = new PeriodicHealthCheckRunner([node], checker, FastOptions());
        await runner.StartAsync();
        await Task.Delay(200);
        await runner.StopAsync();

        Assert.True(node.IsHealthy);
    }

    [Fact]
    public async Task UnhealthyResponse_MarksNodeUnhealthy()
    {
        var node = new BackendNode("n1", "http://a", registeredAt: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1));
        node.MarkHealthy(); // starts healthy

        var checker = new FakeHealthChecker(new() { ["n1"] = false });

        await using var runner = new PeriodicHealthCheckRunner([node], checker, FastOptions());
        await runner.StartAsync();
        await Task.Delay(200);
        await runner.StopAsync();

        Assert.False(node.IsHealthy);
    }

    [Fact]
    public async Task RecoveredNode_BecomesHealthyAgain()
    {
        var node = new BackendNode("n1", "http://a", registeredAt: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1));
        node.MarkUnhealthy();

        var checker = new FakeHealthChecker(new() { ["n1"] = true });

        await using var runner = new PeriodicHealthCheckRunner([node], checker, FastOptions());
        await runner.StartAsync();
        await Task.Delay(200);
        await runner.StopAsync();

        Assert.True(node.IsHealthy);
    }

    [Fact]
    public async Task MultipleNodes_AreAllProbed()
    {
        var nodes = Enumerable.Range(1, 3)
            .Select(i => new BackendNode($"n{i}", $"http://node{i}",
                registeredAt: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1)))
            .ToList();

        var results = nodes.ToDictionary(n => n.Id, _ => true);
        var checker = new FakeHealthChecker(results);

        await using var runner = new PeriodicHealthCheckRunner(nodes, checker, FastOptions());
        await runner.StartAsync();
        await Task.Delay(200);
        await runner.StopAsync();

        // At least one tick fired and every node was checked at least once
        Assert.True(checker.CallCount >= nodes.Count);
        Assert.All(nodes, n => Assert.True(n.IsHealthy));
    }
}
