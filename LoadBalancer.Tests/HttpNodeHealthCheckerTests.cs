using System.Net;
using LoadBalancer.Domain.Entities;
using LoadBalancer.Infrastructure.HealthChecks;

namespace LoadBalancer.Tests;

// =============================================================================
// HttpNodeHealthChecker — HttpMessageHandler stubbed so no real HTTP calls.
// =============================================================================

/// <summary>
/// Minimal fake HttpMessageHandler: returns a fixed status code.
/// We can't use NSubstitute here because SendAsync is protected.
/// </summary>
file sealed class StubHttpMessageHandler(HttpStatusCode status) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(status));
}

/// <summary>
/// Throws the given exception instead of returning a response.
/// </summary>
file sealed class ThrowingHttpMessageHandler(Exception ex) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => throw ex;
}

/// <summary>Captures the last request URI so we can assert the URL shape.</summary>
file sealed class CapturingHttpMessageHandler : HttpMessageHandler
{
    public Uri? LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}

public sealed class HttpNodeHealthCheckerTests
{
    private static HealthCheckOptions Options(string path = "/health") =>
        new() { HealthPath = path };

    private static BackendNode AnyNode(string address = "http://backend:8080") =>
        new("n1", address);

    // --- constructor guards ---

    [Fact]
    public void Constructor_NullHttpClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new HttpNodeHealthChecker(null!, Options()));
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new HttpNodeHealthChecker(new HttpClient(), null!));
    }

    // --- CheckAsync null guard ---

    [Fact]
    public async Task CheckAsync_NullNode_Throws()
    {
        var checker = new HttpNodeHealthChecker(
            new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK)), Options());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => checker.CheckAsync(null!));
    }

    // --- HTTP status codes ---

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.Accepted)]
    public async Task CheckAsync_SuccessStatusCode_ReturnsTrue(HttpStatusCode status)
    {
        var checker = new HttpNodeHealthChecker(
            new HttpClient(new StubHttpMessageHandler(status)), Options());

        Assert.True(await checker.CheckAsync(AnyNode()));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task CheckAsync_NonSuccessStatusCode_ReturnsFalse(HttpStatusCode status)
    {
        var checker = new HttpNodeHealthChecker(
            new HttpClient(new StubHttpMessageHandler(status)), Options());

        Assert.False(await checker.CheckAsync(AnyNode()));
    }

    // --- exception handling ---

    [Fact]
    public async Task CheckAsync_HttpRequestException_ReturnsFalse()
    {
        var checker = new HttpNodeHealthChecker(
            new HttpClient(new ThrowingHttpMessageHandler(new HttpRequestException("connection refused"))),
            Options());

        Assert.False(await checker.CheckAsync(AnyNode()));
    }

    [Fact]
    public async Task CheckAsync_TaskCanceledException_ReturnsFalse()
    {
        var checker = new HttpNodeHealthChecker(
            new HttpClient(new ThrowingHttpMessageHandler(new TaskCanceledException())),
            Options());

        Assert.False(await checker.CheckAsync(AnyNode()));
    }

    [Fact]
    public async Task CheckAsync_OperationCanceledException_ReturnsFalse()
    {
        var checker = new HttpNodeHealthChecker(
            new HttpClient(new ThrowingHttpMessageHandler(new OperationCanceledException())),
            Options());

        Assert.False(await checker.CheckAsync(AnyNode()));
    }

    // --- URL construction ---

    [Fact]
    public async Task CheckAsync_BuildsCorrectUrl_WithTrailingSlash()
    {
        var handler = new CapturingHttpMessageHandler();
        var checker = new HttpNodeHealthChecker(new HttpClient(handler), Options("/health"));

        await checker.CheckAsync(new BackendNode("n1", "http://backend:8080/"));

        Assert.Equal("http://backend:8080/health", handler.LastRequestUri!.ToString());
    }

    [Fact]
    public async Task CheckAsync_BuildsCorrectUrl_WithoutTrailingSlash()
    {
        var handler = new CapturingHttpMessageHandler();
        var checker = new HttpNodeHealthChecker(new HttpClient(handler), Options("/health"));

        await checker.CheckAsync(new BackendNode("n1", "http://backend:8080"));

        Assert.Equal("http://backend:8080/health", handler.LastRequestUri!.ToString());
    }

    [Fact]
    public async Task CheckAsync_UsesCustomHealthPath()
    {
        var handler = new CapturingHttpMessageHandler();
        var checker = new HttpNodeHealthChecker(new HttpClient(handler), Options("/ping"));

        await checker.CheckAsync(new BackendNode("n1", "http://svc"));

        Assert.Equal("http://svc/ping", handler.LastRequestUri!.ToString());
    }
}
