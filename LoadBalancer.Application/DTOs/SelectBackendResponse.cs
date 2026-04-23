namespace LoadBalancer.Application.DTOs;

/// <summary>
/// Result of a backend-selection operation.
/// <see cref="Success"/> is false when no healthy nodes are available.
/// </summary>
public sealed record SelectBackendResponse(
    bool Success,
    BackendNodeDto? Node,
    string? ErrorMessage);
