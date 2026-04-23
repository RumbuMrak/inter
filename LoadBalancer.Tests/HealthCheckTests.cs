using LoadBalancer.Application.Interfaces;
using LoadBalancer.Domain.Entities;
using LoadBalancer.Infrastructure.HealthChecks;
using NSubstitute;

namespace LoadBalancer.Tests;

// =============================================================================
// PeriodicHealthCheckRunner — INodeHealthChecker is mocked via NSubstitute
// =============================================================================

public sealed class PeriodicHealthCheckRunnerConstructorTests
{
    private static INodeHealthChecker AnyChecker() => Substitute.For<INodeHealthChecker>();
    private static HealthCheckOptions AnyOptions() => new();

    [Fact]
    public void Constructor_NullNodes_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PeriodicHealthCheckRunner(null!, AnyChecker(), AnyOptions()));
    }

    [Fact]
    public void Constructor_NullChecker_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PeriodicHealthCheckRunner([], null!, AnyOptions()));
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PeriodicHealthCheckRunner([], AnyChecker(), null!));
    }

    [Fact]
    public async Task StartAsync_CalledTwice_ThrowsObjectDisposedException()
    {
        var runner = new PeriodicHealthCheckRunner([], AnyChecker(), AnyOptions());
        await runner.StartAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => runner.StartAsync());
        await runner.StopAsync();
    }
}

public sealed class PeriodicHealthCheckRunnerGracePeriodTests
{
    [Fact]
    public async Task NodeInGracePeriod_CheckerIsNeverCalled()
    {
        var node    = new BackendNode("n1", "http://a"); // RegisteredAt = UtcNow
        var checker = Substitute.For<INodeHealthChecker>();
        var options = new HealthCheckOptions
        {
            GracePeriod = TimeSpan.FromHours(1),
            Interval    = TimeSpan.FromMilliseconds(50),
            Timeout     = TimeSpan.FromSeconds(1),
        };

        await using var runner = new PeriodicHealthCheckRunner([node], checker, options);
        await runner.StartAsync();
        await Task.Delay(200);
        await runner.StopAsync();

        await checker.DidNotReceive().CheckAsync(Arg.Any<BackendNode>(), Arg.Any<CancellationToken>());
        Assert.True(node.IsHealthy); // untouched — stays at initial healthy state
    }

    [Fact]
    public async Task NodePastGracePeriod_CheckerIsCalledAtLeastOnce()
    {
        var node = new BackendNode("n1", "http://a",
            registeredAt: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1));
        var checker = Substitute.For<INodeHealthChecker>();
        checker.CheckAsync(Arg.Any<BackendNode>(), Arg.Any<CancellationToken>())
               .Returns(true);

        var options = new HealthCheckOptions
        {
            GracePeriod = TimeSpan.FromSeconds(5),
            Interval    = TimeSpan.FromMilliseconds(50),
            Timeout     = TimeSpan.FromSeconds(1),
        };

        await using var runner = new PeriodicHealthCheckRunner([node], checker, options);
        await runner.StartAsync();
        await Task.Delay(200);
        await runner.StopAsync();

        await checker.Received().CheckAsync(node, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmptyNodeList_CheckerIsNeverCalled()
    {
        var checker = Substitute.For<INodeHealthChecker>();
        var options = new HealthCheckOptions
        {
            GracePeriod = TimeSpan.Zero,
            Interval    = TimeSpan.FromMilliseconds(50),
            Timeout     = TimeSpan.FromSeconds(1),
        };

        await using var runner = new PeriodicHealthCheckRunner([], checker, options);
        await runner.StartAsync();
        await Task.Delay(200);
        await runner.StopAsync();

        await checker.DidNotReceive().CheckAsync(Arg.Any<BackendNode>(), Arg.Any<CancellationToken>());
    }
}

public sealed class PeriodicHealthCheckRunnerHealthStateTests
{
    private static BackendNode PastGraceNode(string id = "n1") =>
        new(id, $"http://{id}", registeredAt: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1));

    private static HealthCheckOptions FastOptions() => new()
    {
        GracePeriod = TimeSpan.Zero,
        Interval    = TimeSpan.FromMilliseconds(50),
        Timeout     = TimeSpan.FromSeconds(1),
    };

    [Fact]
    public async Task CheckerReturnsTrue_MarksNodeHealthy()
    {
        var node    = PastGraceNode();
        var checker = Substitute.For<INodeHealthChecker>();
        checker.CheckAsync(node, Arg.Any<CancellationToken>()).Returns(true);

        await using var runner = new PeriodicHealthCheckRunner([node], checker, FastOptions());
        await runner.StartAsync();
        await Task.Delay(200);
        await runner.StopAsync();

        Assert.True(node.IsHealthy);
    }

    [Fact]
    public async Task CheckerReturnsFalse_MarksNodeUnhealthy()
    {
        var node = PastGraceNode();
        var checker = Substitute.For<INodeHealthChecker>();
        checker.CheckAsync(node, Arg.Any<CancellationToken>()).Returns(false);

        await using var runner = new PeriodicHealthCheckRunner([node], checker, FastOptions());
        await runner.StartAsync();
        await Task.Delay(200);
        await runner.StopAsync();

        Assert.False(node.IsHealthy);
    }

    [Fact]
    public async Task CheckerTransitionsFromFalseToTrue_NodeRecovers()
    {
        var node    = PastGraceNode();
        node.MarkUnhealthy();
        var checker = Substitute.For<INodeHealthChecker>();
        checker.CheckAsync(node, Arg.Any<CancellationToken>()).Returns(true);

        await using var runner = new PeriodicHealthCheckRunner([node], checker, FastOptions());
        await runner.StartAsync();
        await Task.Delay(200);
        await runner.StopAsync();

        Assert.True(node.IsHealthy);
    }

    [Fact]
    public async Task MultipleNodes_CheckerCalledForEach()
    {
        var nodes = new[]
        {
            PastGraceNode("n1"),
            PastGraceNode("n2"),
            PastGraceNode("n3"),
        };

        var checker = Substitute.For<INodeHealthChecker>();
        checker.CheckAsync(Arg.Any<BackendNode>(), Arg.Any<CancellationToken>()).Returns(true);

        await using var runner = new PeriodicHealthCheckRunner(nodes, checker, FastOptions());
        await runner.StartAsync();
        await Task.Delay(200);
        await runner.StopAsync();

        foreach (var node in nodes)
            await checker.Received().CheckAsync(node, Arg.Any<CancellationToken>());
    }
}
