using LoadBalancer.Domain.Entities;
using LoadBalancer.Infrastructure.Strategies;

namespace LoadBalancer.Tests;

public sealed class LeastOutstandingRequestsLoadBalancerTests
{
    private static BackendNode Node(string id) =>
        new(id, $"http://{id}", registeredAt: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1));

    // --- constructor guards ---

    [Fact]
    public void Constructor_NullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LeastOutstandingRequestsLoadBalancer(null!));
    }

    // --- Next() edge cases ---

    [Fact]
    public void Next_EmptyList_ReturnsNull()
    {
        Assert.Null(new LeastOutstandingRequestsLoadBalancer([]).Next());
    }

    [Fact]
    public void Next_AllUnhealthy_ReturnsNull()
    {
        var node = Node("a");
        node.MarkUnhealthy();
        Assert.Null(new LeastOutstandingRequestsLoadBalancer([node]).Next());
    }

    [Fact]
    public void Next_SingleHealthyNode_ReturnsThatNode()
    {
        var node = Node("a");
        Assert.Equal("a", new LeastOutstandingRequestsLoadBalancer([node]).Next()!.Id);
    }

    // --- Track() edge cases ---

    [Fact]
    public void Track_EmptyList_ReturnsNull()
    {
        Assert.Null(new LeastOutstandingRequestsLoadBalancer([]).Track());
    }

    [Fact]
    public void Track_AllUnhealthy_ReturnsNull()
    {
        var node = Node("a");
        node.MarkUnhealthy();
        Assert.Null(new LeastOutstandingRequestsLoadBalancer([node]).Track());
    }

    // --- LOR selection logic ---

    [Fact]
    public void Track_PicksNodeWithFewestActiveRequests()
    {
        var a = Node("a");
        var b = Node("b");
        var c = Node("c");
        var lb = new LeastOutstandingRequestsLoadBalancer([a, b, c]);

        using var s1 = lb.Track(); // all at 0 → a (first)
        using var s2 = lb.Track(); // a=1 b=0 c=0 → b
        using var s3 = lb.Track(); // a=1 b=1 c=0 → c
        using var s4 = lb.Track(); // all at 1 → a (first, tie-break)

        Assert.Equal("a", s1!.Node.Id);
        Assert.Equal("b", s2!.Node.Id);
        Assert.Equal("c", s3!.Node.Id);
        Assert.Equal("a", s4!.Node.Id);
    }

    [Fact]
    public void Track_UnhealthyNodeSkipped_EvenWithZeroActive()
    {
        var a = Node("a");
        var b = Node("b");
        b.MarkUnhealthy();

        // Give a one in-flight request so it has more than b's 0 — but b is unhealthy.
        using var _ = new LeastOutstandingRequestsLoadBalancer([a, b]).Track();

        // Fresh lb instance for the assertion to avoid counter pollution
        var lb    = new LeastOutstandingRequestsLoadBalancer([a, b]);
        var scope = lb.Track();

        Assert.Equal("a", scope!.Node.Id);
    }

    // --- IRequestScope / counter lifecycle ---

    [Fact]
    public void Track_IncreasesActiveRequests_OnAcquire()
    {
        var node = Node("a");
        var scope = new LeastOutstandingRequestsLoadBalancer([node]).Track()!;

        Assert.Equal(1, node.ActiveRequests);
        scope.Dispose();
    }

    [Fact]
    public void Track_DecrementsActiveRequests_OnDispose()
    {
        var node  = Node("a");
        var scope = new LeastOutstandingRequestsLoadBalancer([node]).Track()!;
        scope.Dispose();

        Assert.Equal(0, node.ActiveRequests);
    }

    [Fact]
    public void Track_DoubleDispose_IsIdempotent()
    {
        var node  = Node("a");
        var scope = new LeastOutstandingRequestsLoadBalancer([node]).Track()!;
        scope.Dispose();
        scope.Dispose(); // must not go negative

        Assert.Equal(0, node.ActiveRequests);
    }

    [Fact]
    public void Track_ScopeNodeProperty_MatchesSelectedNode()
    {
        var node  = Node("my-node");
        using var scope = new LeastOutstandingRequestsLoadBalancer([node]).Track()!;

        Assert.Equal("my-node", scope.Node.Id);
    }

    // --- thread safety ---

    [Fact]
    public void Track_ConcurrentAcquireAndRelease_CounterRemainsConsistent()
    {
        var nodes = Enumerable.Range(0, 4).Select(i => Node($"n{i}")).ToList();
        var lb    = new LeastOutstandingRequestsLoadBalancer(nodes);

        var scopes = Enumerable.Range(0, 100)
            .AsParallel()
            .Select(_ => lb.Track())
            .ToList();

        Assert.Equal(100, nodes.Sum(n => n.ActiveRequests));

        foreach (var s in scopes) s?.Dispose();

        Assert.All(nodes, n => Assert.Equal(0, n.ActiveRequests));
    }
}

