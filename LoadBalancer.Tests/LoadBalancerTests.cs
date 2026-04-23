using LoadBalancer.Application.DTOs;
using LoadBalancer.Application.Services;
using LoadBalancer.Domain.Entities;
using LoadBalancer.Infrastructure.Strategies;

namespace LoadBalancer.Tests;

public sealed class RoundRobinLoadBalancerTests
{
    private static RoundRobinLoadBalancer BuildBalancer(params string[] addresses)
    {
        var nodes = addresses
            .Select((addr, i) => new BackendNode($"node-{i}", addr))
            .ToList();
        return new RoundRobinLoadBalancer(nodes);
    }

    [Fact]
    public void Next_SingleNode_AlwaysReturnsSameNode()
    {
        var balancer = BuildBalancer("http://a:8080");

        var first = balancer.Next();
        var second = balancer.Next();

        Assert.Equal(first!.Address, second!.Address);
    }

    [Fact]
    public void Next_ThreeNodes_CyclesInOrder()
    {
        var balancer = BuildBalancer("A", "B", "C");

        var results = Enumerable.Range(0, 6).Select(_ => balancer.Next()!.Address).ToList();

        Assert.Equal(["A", "B", "C", "A", "B", "C"], results);
    }

    [Fact]
    public void Next_NoNodes_ReturnsNull()
    {
        var balancer = new RoundRobinLoadBalancer([]);

        Assert.Null(balancer.Next());
    }

    [Fact]
    public void Next_AllNodesUnhealthy_ReturnsNull()
    {
        var node = new BackendNode("n1", "http://a:80");
        node.MarkUnhealthy();
        var balancer = new RoundRobinLoadBalancer([node]);

        Assert.Null(balancer.Next());
    }

    [Fact]
    public void Next_SkipsUnhealthyNodes()
    {
        var nodeA = new BackendNode("n-a", "A");
        var nodeB = new BackendNode("n-b", "B");
        var nodeC = new BackendNode("n-c", "C");
        nodeB.MarkUnhealthy();

        var balancer = new RoundRobinLoadBalancer([nodeA, nodeB, nodeC]);

        var results = Enumerable.Range(0, 4).Select(_ => balancer.Next()!.Address).ToList();

        Assert.All(results, r => Assert.NotEqual("B", r));
        Assert.Contains("A", results);
        Assert.Contains("C", results);
    }
}

public sealed class LoadBalancerServiceTests
{
    [Fact]
    public void SelectBackend_ReturnsSuccessResponse_WhenNodeAvailable()
    {
        var node = new BackendNode("n1", "http://backend:8080");
        var balancer = new RoundRobinLoadBalancer([node]);
        var service = new LoadBalancerService(balancer);

        var response = service.SelectBackend(new SelectBackendRequest());

        Assert.True(response.Success);
        Assert.NotNull(response.Node);
        Assert.Equal("http://backend:8080", response.Node.Address);
        Assert.Null(response.ErrorMessage);
    }

    [Fact]
    public void SelectBackend_ReturnsFailureResponse_WhenNoNodes()
    {
        var balancer = new RoundRobinLoadBalancer([]);
        var service = new LoadBalancerService(balancer);

        var response = service.SelectBackend(new SelectBackendRequest());

        Assert.False(response.Success);
        Assert.Null(response.Node);
        Assert.NotNull(response.ErrorMessage);
    }

    [Fact]
    public void SelectBackend_ThrowsArgumentNull_WhenRequestIsNull()
    {
        var balancer = new RoundRobinLoadBalancer([new BackendNode("n1", "http://x")]);
        var service = new LoadBalancerService(balancer);

        Assert.Throws<ArgumentNullException>(() => service.SelectBackend(null!));
    }
}
