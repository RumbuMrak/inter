using LoadBalancer.Application.DTOs;
using LoadBalancer.Application.Interfaces;
using LoadBalancer.Domain.Entities;
using LoadBalancer.Domain.Interfaces;
using NSubstitute;

namespace LoadBalancer.Tests;

// =============================================================================
// ILoadBalancerService mock — demonstrates how upper layers (e.g. controllers)
// can be tested without any infrastructure dependency.
// =============================================================================

public sealed class ILoadBalancerServiceMockTests
{
    [Fact]
    public void MockedService_ReturnsConfiguredSuccessResponse()
    {
        var service = Substitute.For<ILoadBalancerService>();
        var dto     = new BackendNodeDto("n1", "http://host:80");
        service.SelectBackend(Arg.Any<SelectBackendRequest>())
               .Returns(new SelectBackendResponse(true, dto, null));

        var result = service.SelectBackend(new SelectBackendRequest());

        Assert.True(result.Success);
        Assert.Equal("n1", result.Node!.Id);
    }

    [Fact]
    public void MockedService_ReturnsConfiguredFailureResponse()
    {
        var service = Substitute.For<ILoadBalancerService>();
        service.SelectBackend(Arg.Any<SelectBackendRequest>())
               .Returns(new SelectBackendResponse(false, null, "no nodes"));

        var result = service.SelectBackend(new SelectBackendRequest());

        Assert.False(result.Success);
        Assert.Equal("no nodes", result.ErrorMessage);
    }

    [Fact]
    public void MockedService_VerifiesSelectBackendWasCalled()
    {
        var service = Substitute.For<ILoadBalancerService>();
        service.SelectBackend(Arg.Any<SelectBackendRequest>())
               .Returns(new SelectBackendResponse(false, null, "empty"));

        service.SelectBackend(new SelectBackendRequest());
        service.SelectBackend(new SelectBackendRequest());

        service.Received(2).SelectBackend(Arg.Any<SelectBackendRequest>());
    }
}

// =============================================================================
// IHealthCheckRunner mock — demonstrates lifecycle contract verification.
// =============================================================================

public sealed class IHealthCheckRunnerMockTests
{
    [Fact]
    public async Task MockedRunner_StartAsync_IsReceived()
    {
        var runner = Substitute.For<IHealthCheckRunner>();

        await runner.StartAsync();

        await runner.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MockedRunner_StopAsync_IsReceived()
    {
        var runner = Substitute.For<IHealthCheckRunner>();

        await runner.StartAsync();
        await runner.StopAsync();

        await runner.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MockedRunner_StopNotCalledIfNeverStarted()
    {
        var runner = Substitute.For<IHealthCheckRunner>();

        await runner.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
    }
}

// =============================================================================
// IRequestScope mock — demonstrates scoped request tracking contract.
// =============================================================================

public sealed class IRequestScopeMockTests
{
    [Fact]
    public void MockedScope_ExposesNode()
    {
        var node  = new BackendNode("n1", "http://host");
        var scope = Substitute.For<IRequestScope>();
        scope.Node.Returns(node);

        Assert.Equal("n1", scope.Node.Id);
    }

    [Fact]
    public void MockedScope_DisposeIsCalled_WhenUsedInUsingBlock()
    {
        var scope = Substitute.For<IRequestScope>();
        scope.Node.Returns(new BackendNode("n1", "http://host"));

        using (scope) { /* simulate a request */ }

        scope.Received(1).Dispose();
    }

    [Fact]
    public void MockedLoadBalancer_Track_ReturnsConfiguredScope()
    {
        var node     = new BackendNode("n1", "http://host");
        var scope    = Substitute.For<IRequestScope>();
        scope.Node.Returns(node);

        var balancer = Substitute.For<ILoadBalancer>();
        balancer.Track().Returns(scope);

        using var result = balancer.Track();

        Assert.Equal("n1", result!.Node.Id);
        balancer.Received(1).Track();
    }
}
