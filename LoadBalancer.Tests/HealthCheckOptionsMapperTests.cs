using LoadBalancer.Application.DTOs;
using LoadBalancer.Application.Mapping;
using LoadBalancer.Domain.Entities;

namespace LoadBalancer.Tests;

public sealed class HealthCheckOptionsMapperTests
{
    private static readonly HealthCheckOptions Defaults = new();

    [Fact]
    public void ToDomain_NullDto_ReturnsAllDefaults()
    {
        var result = HealthCheckOptionsMapper.ToDomain(null);

        Assert.Equal(Defaults.GracePeriod, result.GracePeriod);
        Assert.Equal(Defaults.Interval,    result.Interval);
        Assert.Equal(Defaults.Timeout,     result.Timeout);
        Assert.Equal(Defaults.HealthPath,  result.HealthPath);
    }

    [Fact]
    public void ToDomain_EmptyDto_ReturnsAllDefaults()
    {
        var result = HealthCheckOptionsMapper.ToDomain(new HealthCheckOptionsDto());

        Assert.Equal(Defaults.GracePeriod, result.GracePeriod);
        Assert.Equal(Defaults.Interval,    result.Interval);
        Assert.Equal(Defaults.Timeout,     result.Timeout);
        Assert.Equal(Defaults.HealthPath,  result.HealthPath);
    }

    [Fact]
    public void ToDomain_AllPropertiesSupplied_UsesCallerValues()
    {
        var dto = new HealthCheckOptionsDto(
            GracePeriod: TimeSpan.FromSeconds(30),
            Interval:    TimeSpan.FromSeconds(60),
            Timeout:     TimeSpan.FromSeconds(3),
            HealthPath:  "/ping");

        var result = HealthCheckOptionsMapper.ToDomain(dto);

        Assert.Equal(TimeSpan.FromSeconds(30), result.GracePeriod);
        Assert.Equal(TimeSpan.FromSeconds(60), result.Interval);
        Assert.Equal(TimeSpan.FromSeconds(3),  result.Timeout);
        Assert.Equal("/ping",                  result.HealthPath);
    }

    [Fact]
    public void ToDomain_PartialDto_MixesCallerAndDefaults()
    {
        // Only GracePeriod and HealthPath provided; Interval and Timeout fall back
        var dto = new HealthCheckOptionsDto(
            GracePeriod: TimeSpan.FromSeconds(5),
            HealthPath:  "/ready");

        var result = HealthCheckOptionsMapper.ToDomain(dto);

        Assert.Equal(TimeSpan.FromSeconds(5), result.GracePeriod);   // caller
        Assert.Equal(Defaults.Interval,       result.Interval);       // default
        Assert.Equal(Defaults.Timeout,        result.Timeout);        // default
        Assert.Equal("/ready",                result.HealthPath);     // caller
    }
}
