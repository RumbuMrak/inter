using LoadBalancer.Application.Interfaces;
using LoadBalancer.Domain.Entities;

namespace LoadBalancer.Infrastructure.HealthChecks;

/// <summary>
/// Probes a backend node's health endpoint via HTTP GET.
/// A 2xx response means the node is healthy; any other response or
/// exception (network error, timeout) marks it unhealthy.
/// </summary>
public sealed class HttpNodeHealthChecker : INodeHealthChecker
{
    private readonly HttpClient _httpClient;
    private readonly string _healthPath;

    /// <param name="httpClient">Shared <see cref="HttpClient"/> instance (caller manages lifetime).</param>
    /// <param name="options">Resolved health check configuration. Use <c>HealthCheckOptionsMapper.ToDomain(...)</c> at the entry point to guarantee a non-null value.</param>
    public HttpNodeHealthChecker(HttpClient httpClient, HealthCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _healthPath = options.HealthPath;
    }

    /// <inheritdoc/>
    public async Task<bool> CheckAsync(BackendNode node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        var url = $"{node.Address.TrimEnd('/')}{_healthPath}";

        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return false;
        }
    }
}
