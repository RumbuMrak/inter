namespace LoadBalancer.Application.DTOs;

/// <summary>
/// Input DTO for selecting the next backend. Extendable with request metadata
/// (e.g. client IP, path) for future routing strategies.
/// </summary>
public sealed record SelectBackendRequest(string? ClientHint = null);
