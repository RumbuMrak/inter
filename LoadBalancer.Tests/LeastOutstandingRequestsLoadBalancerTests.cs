using LoadBalancer.Domain.Entities;
using LoadBalancer.Infrastructure.Strategies;

namespace LoadBalancer.Tests;

public sealed class LeastOutstandingRequestsLoadBalancerTests
{
    private static BackendNode Node(string id) =>
        new(id, $"http://{id}", registeredAt: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1));

    [Fact]
    public void Next_NoNodes_ReturnsNull()
    {
        var lb = new LeastOutstandingRequestsLoadBalancer([]);
        Assert.Null(lb.Next());
    }

    [Fact]
    public void Next_AllUnhealthy_ReturnsNull()
    {
        var node = Node("a");
        node.MarkUnhealthy();
        var lb = new LeastOutstandingRequestsLoadBalancer([node]);
        Assert.Null(lb.Next());
    }

    [Fact]
    public void Next_SingleNode_ReturnsThatNode()
    {
        var node = Node("a");
        var lb = new LeastOutstandingRequestsLoadBalancer([node]);
        Assert.Equal("a", lb.Next()!.Id);
    }

    [Fact]
    public void Track_PicksNodeWithFewestActive()
    {
        var a = Node("a");
        var b = Node("b");
        var c = Node("c");

        var lb = new LeastOutstandingRequestsLoadBalancer([a, b, c]);

        // Pre-load a and c with in-flight requests; b stays at 0.
        using var s1 = lb.Track(); // → a (all tied at 0, picks first)
        using var s2 = lb.Track(); // → b (a=1, b=0, c=0 → picks b)
        using var s3 = lb.Track(); // → c (a=1, b=1, c=0 → picks c)
        using var s4 = lb.Track(); // → a (all tied at 1, picks first again)

        Assert.Equal("a", s1!.Node.Id);
        Assert.Equal("b", s2!.Node.Id);
        Assert.Equal("c", s3!.Node.Id);
        Assert.Equal("a", s4!.Node.Id);
    }

    [Fact]
    public void Track_DisposingScope_DecrementsCounter()
    {
        var node = Node("a");
        var lb = new LeastOutstandingRequestsLoadBalancer([node]);

        var scope = lb.Track()!;
        Assert.Equal(1, node.ActiveRequests);

        scope.Dispose();
        Assert.Equal(0, node.ActiveRequests);
    }

    [Fact]
    public void Track_DoubleDispose_DoesNotDoubleDecrement()
    {
        var node = Node("a");
        var lb = new LeastOutstandingRequestsLoadBalancer([node]);

        var scope = lb.Track()!;
        scope.Dispose();
        scope.Dispose(); // safe — should not go negative

        Assert.Equal(0, node.ActiveRequests);
    }

    [Fact]
    public void Track_UnhealthyNodeSkipped_EvenIfFewerActive()
    {
        var a = Node("a");
        var b = Node("b");
        b.MarkUnhealthy();

        // a has 1 active, b has 0 but is unhealthy — must still pick a
        using var _ = new LeastOutstandingRequestsLoadBalancer([a, b]).Track();

        var lb = new LeastOutstandingRequestsLoadBalancer([a, b]);
        var scope = lb.Track();
        Assert.Equal("a", scope!.Node.Id);
    }

    [Fact]
    public void Track_ConcurrentRequests_CounterStaysConsistent()
    {
        var nodes = Enumerable.Range(0, 4).Select(i => Node($"n{i}")).ToList();
        var lb = new LeastOutstandingRequestsLoadBalancer(nodes);

        // Open 100 scopes concurrently, then close them all.
        var scopes = Enumerable.Range(0, 100)
            .AsParallel()
            .Select(_ => lb.Track())
            .ToList();

        var totalActive = nodes.Sum(n => n.ActiveRequests);
        Assert.Equal(100, totalActive);

        foreach (var s in scopes) s?.Dispose();

        Assert.All(nodes, n => Assert.Equal(0, n.ActiveRequests));
    }
}
