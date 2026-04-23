using LoadBalancer.Application.DTOs;
using LoadBalancer.Application.Services;
using LoadBalancer.Domain.Entities;
using LoadBalancer.Domain.Interfaces;
using LoadBalancer.Infrastructure.Strategies;
using NSubstitute;

namespace LoadBalancer.Tests;

// =============================================================================
// LoadBalancerService — tested through mocked ILoadBalancer
// =============================================================================

public sealed class LoadBalancerServiceTests
{
    private static BackendNode AnyNode(string id = "n1") =>
        new(id, $"http://{id}:8080");

    // --- happy path ---

    [Fact]
    public void SelectBackend_WhenNodeAvailable_ReturnsSuccessDto()
    {
        var node     = AnyNode("n1");
        var balancer = Substitute.For<ILoadBalancer>();
        balancer.Next().Returns(node);

        var response = new LoadBalancerService(balancer).SelectBackend(new SelectBackendRequest());

        Assert.True(response.Success);
        Assert.NotNull(response.Node);
        Assert.Equal("n1",              response.Node.Id);
        Assert.Equal("http://n1:8080",  response.Node.Address);
        Assert.Null(response.ErrorMessage);
    }

    [Fact]
    public void SelectBackend_CallsNextExactlyOnce()
    {
        var balancer = Substitute.For<ILoadBalancer>();
        balancer.Next().Returns(AnyNode());

        new LoadBalancerService(balancer).SelectBackend(new SelectBackendRequest());

        balancer.Received(1).Next();
    }

    [Fact]
    public void SelectBackend_NeverCallsTrack()
    {
        // Service uses Next(), not Track() — verify Track is untouched
        var balancer = Substitute.For<ILoadBalancer>();
        balancer.Next().Returns(AnyNode());

        new LoadBalancerService(balancer).SelectBackend(new SelectBackendRequest());

        balancer.DidNotReceive().Track();
    }

    // --- no healthy nodes ---

    [Fact]
    public void SelectBackend_WhenNextReturnsNull_ReturnsFailureDto()
    {
        var balancer = Substitute.For<ILoadBalancer>();
        balancer.Next().Returns((BackendNode?)null);

        var response = new LoadBalancerService(balancer).SelectBackend(new SelectBackendRequest());

        Assert.False(response.Success);
        Assert.Null(response.Node);
        Assert.NotEmpty(response.ErrorMessage!);
    }

    // --- null guards ---

    [Fact]
    public void Constructor_NullBalancer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LoadBalancerService(null!));
    }

    [Fact]
    public void SelectBackend_NullRequest_Throws()
    {
        var balancer = Substitute.For<ILoadBalancer>();
        Assert.Throws<ArgumentNullException>(
            () => new LoadBalancerService(balancer).SelectBackend(null!));
    }

    // --- DTO mapping ---

    [Fact]
    public void SelectBackend_MapsIdAndAddressToDto()
    {
        var node     = new BackendNode("my-id", "http://192.168.1.1:9000");
        var balancer = Substitute.For<ILoadBalancer>();
        balancer.Next().Returns(node);

        var dto = new LoadBalancerService(balancer).SelectBackend(new SelectBackendRequest()).Node!;

        Assert.Equal("my-id",                  dto.Id);
        Assert.Equal("http://192.168.1.1:9000", dto.Address);
    }
}

// =============================================================================
// RoundRobinLoadBalancer — concrete implementation tests
// =============================================================================

public sealed class RoundRobinLoadBalancerTests
{
    private static RoundRobinLoadBalancer Build(params string[] addresses)
    {
        var nodes = addresses.Select((a, i) => new BackendNode($"n{i}", a)).ToList();
        return new RoundRobinLoadBalancer(nodes);
    }

    // --- Next() ---

    [Fact]
    public void Next_EmptyList_ReturnsNull()
    {
        Assert.Null(new RoundRobinLoadBalancer([]).Next());
    }

    [Fact]
    public void Next_SingleNode_AlwaysReturnsSameNode()
    {
        var lb = Build("http://a");
        Assert.Equal(lb.Next()!.Address, lb.Next()!.Address);
    }

    [Fact]
    public void Next_ThreeNodes_CyclesInOrder()
    {
        var lb      = Build("A", "B", "C");
        var results = Enumerable.Range(0, 6).Select(_ => lb.Next()!.Address).ToList();
        Assert.Equal(["A", "B", "C", "A", "B", "C"], results);
    }

    [Fact]
    public void Next_AllNodesUnhealthy_ReturnsNull()
    {
        var node = new BackendNode("n1", "http://a");
        node.MarkUnhealthy();
        Assert.Null(new RoundRobinLoadBalancer([node]).Next());
    }

    [Fact]
    public void Next_SkipsUnhealthyNodes()
    {
        var a = new BackendNode("a", "A");
        var b = new BackendNode("b", "B");
        var c = new BackendNode("c", "C");
        b.MarkUnhealthy();

        var results = Enumerable.Range(0, 4)
            .Select(_ => new RoundRobinLoadBalancer([a, b, c]).Next()!.Address)
            .ToList();

        Assert.DoesNotContain("B", results);
    }

    [Fact]
    public void Constructor_NullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RoundRobinLoadBalancer(null!));
    }

    // --- Track() ---

    [Fact]
    public void Track_EmptyList_ReturnsNull()
    {
        Assert.Null(new RoundRobinLoadBalancer([]).Track());
    }

    [Fact]
    public void Track_ReturnsScope_WithSelectedNode()
    {
        var node = new BackendNode("n1", "http://a");
        using var scope = new RoundRobinLoadBalancer([node]).Track();
        Assert.Equal("n1", scope!.Node.Id);
    }

    [Fact]
    public void Track_IncreasesActiveRequests_ThenDecrementsOnDispose()
    {
        var node = new BackendNode("n1", "http://a");
        var lb   = new RoundRobinLoadBalancer([node]);

        var scope = lb.Track()!;
        Assert.Equal(1, node.ActiveRequests);

        scope.Dispose();
        Assert.Equal(0, node.ActiveRequests);
    }
}

