namespace LoadBalancer.Application.DTOs;

/// <summary>
/// Data transferred when registering backend nodes with the load balancer.
/// </summary>
public sealed record BackendNodeDto(string Id, string Address);
