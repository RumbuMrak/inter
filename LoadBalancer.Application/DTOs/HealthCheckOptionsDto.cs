namespace LoadBalancer.Application.DTOs;

/// <summary>
/// Optional health-check configuration supplied by an upper layer (e.g. API controller, CLI).
/// Any property left <c>null</c> falls back to the default defined in
/// <see cref="Domain.Entities.HealthCheckOptions"/>.
/// </summary>
public sealed record HealthCheckOptionsDto(
    TimeSpan? GracePeriod = null,
    TimeSpan? Interval    = null,
    TimeSpan? Timeout     = null,
    string?   HealthPath  = null);
