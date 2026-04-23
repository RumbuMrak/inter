using LoadBalancer.Application.DTOs;
using LoadBalancer.Domain.Entities;

namespace LoadBalancer.Application.Mapping;

/// <summary>
/// Maps <see cref="HealthCheckOptionsDto"/> supplied by upper layers into the
/// domain <see cref="HealthCheckOptions"/>, filling any omitted values with
/// the domain defaults.
/// </summary>
public static class HealthCheckOptionsMapper
{
    private static readonly HealthCheckOptions Defaults = new();

    /// <summary>
    /// Converts an optional <see cref="HealthCheckOptionsDto"/> to a fully-populated
    /// <see cref="HealthCheckOptions"/>. When <paramref name="dto"/> is <c>null</c>,
    /// all domain defaults are used.
    /// </summary>
    public static HealthCheckOptions ToDomain(HealthCheckOptionsDto? dto)
    {
        if (dto is null)
            return Defaults;

        return new HealthCheckOptions
        {
            GracePeriod = dto.GracePeriod ?? Defaults.GracePeriod,
            Interval    = dto.Interval    ?? Defaults.Interval,
            Timeout     = dto.Timeout     ?? Defaults.Timeout,
            HealthPath  = dto.HealthPath  ?? Defaults.HealthPath,
        };
    }
}
