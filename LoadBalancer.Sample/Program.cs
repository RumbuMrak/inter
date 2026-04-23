using LoadBalancer.Application.DTOs;
using LoadBalancer.Application.Mapping;
using LoadBalancer.Application.Services;
using LoadBalancer.Domain.Entities;
using LoadBalancer.Infrastructure.HealthChecks;
using LoadBalancer.Infrastructure.Strategies;

Console.WriteLine("=== Load Balancer Demo ===\n");

// ---------------------------------------------------------------------------
// Setup nodes
// ---------------------------------------------------------------------------
var nodes = new List<BackendNode>
{
    new("node-a", "http://backend-a:8080"),
    new("node-b", "http://backend-b:8080"),
    new("node-c", "http://backend-c:8080"),
};

var balancer = new RoundRobinLoadBalancer(nodes);
var service  = new LoadBalancerService(balancer);

// ---------------------------------------------------------------------------
// Health check configuration supplied by the upper layer (e.g. read from
// appsettings.json, env vars, CLI args).  Any null property falls back to
// the domain defaults (GracePeriod=10s, Interval=15s, Timeout=5s, /health).
//
// Try: pass null instead of the dto to see all defaults applied.
// ---------------------------------------------------------------------------
var callerOptions = new HealthCheckOptionsDto(
    GracePeriod: TimeSpan.FromSeconds(10),
    Interval:    TimeSpan.FromSeconds(5),
    Timeout:     TimeSpan.FromSeconds(2)
    // HealthPath omitted → defaults to "/health"
);

// Application layer maps DTO → domain object, filling gaps with defaults.
var healthOptions = HealthCheckOptionsMapper.ToDomain(callerOptions);

Console.WriteLine("Effective health-check config:");
Console.WriteLine($"  GracePeriod : {healthOptions.GracePeriod.TotalSeconds}s");
Console.WriteLine($"  Interval    : {healthOptions.Interval.TotalSeconds}s");
Console.WriteLine($"  Timeout     : {healthOptions.Timeout.TotalSeconds}s");
Console.WriteLine($"  HealthPath  : {healthOptions.HealthPath}\n");

// HttpClient should be long-lived (one per application, not per request).
using var httpClient = new HttpClient();
var healthChecker = new HttpNodeHealthChecker(httpClient, healthOptions);

await using var runner = new PeriodicHealthCheckRunner(nodes, healthChecker, healthOptions);

Console.WriteLine($"Starting health-check runner (grace period: {healthOptions.GracePeriod.TotalSeconds}s, " +
                  $"interval: {healthOptions.Interval.TotalSeconds}s)...\n");

await runner.StartAsync();

// ---------------------------------------------------------------------------
// Round-robin demo (all nodes healthy — real /health calls would fail here
// since these backends don't exist, so we control health manually below)
// ---------------------------------------------------------------------------
Console.WriteLine("Round-robin across A, B, C (6 requests):");
for (int i = 1; i <= 6; i++)
{
    var response = service.SelectBackend(new SelectBackendRequest());
    Console.WriteLine($"  Request {i} -> {(response.Success ? response.Node!.Address : response.ErrorMessage)}");
}

// ---------------------------------------------------------------------------
// Simulate a node going down (as the health checker would do)
// ---------------------------------------------------------------------------
Console.WriteLine("\n[Simulating] node-b failed its health check → marking unhealthy");
nodes.Single(n => n.Id == "node-b").MarkUnhealthy();

Console.WriteLine("Round-robin skips node-b (4 requests):");
for (int i = 1; i <= 4; i++)
{
    var response = service.SelectBackend(new SelectBackendRequest());
    Console.WriteLine($"  Request {i} -> {(response.Success ? response.Node!.Address : response.ErrorMessage)}");
}

// ---------------------------------------------------------------------------
// Simulate node-b recovering
// ---------------------------------------------------------------------------
Console.WriteLine("\n[Simulating] node-b passed its health check → marking healthy again");
nodes.Single(n => n.Id == "node-b").MarkHealthy();

Console.WriteLine("Round-robin includes node-b again (6 requests):");
for (int i = 1; i <= 6; i++)
{
    var response = service.SelectBackend(new SelectBackendRequest());
    Console.WriteLine($"  Request {i} -> {(response.Success ? response.Node!.Address : response.ErrorMessage)}");
}

// ---------------------------------------------------------------------------
// All nodes unhealthy edge case
// ---------------------------------------------------------------------------
Console.WriteLine("\n[Simulating] all nodes unhealthy:");
foreach (var n in nodes) n.MarkUnhealthy();
var empty = service.SelectBackend(new SelectBackendRequest());
Console.WriteLine($"  -> {empty.ErrorMessage}");

await runner.StopAsync();
Console.WriteLine("\nHealth-check runner stopped. Done.");


Console.WriteLine("=== Load Balancer Demo ===\n");

// --- Setup ---
var nodes = new List<BackendNode>
{
    new("node-a", "http://backend-a:8080"),
    new("node-b", "http://backend-b:8080"),
    new("node-c", "http://backend-c:8080"),
};

var balancer = new RoundRobinLoadBalancer(nodes);
var service  = new LoadBalancerService(balancer);

// --- Round-robin across all three nodes ---
Console.WriteLine("Round-robin across A, B, C (6 requests):");
for (int i = 1; i <= 6; i++)
{
    var response = service.SelectBackend(new SelectBackendRequest());
    Console.WriteLine($"  Request {i} -> {(response.Success ? response.Node!.Address : response.ErrorMessage)}");
}

// --- Mark node-b unhealthy ---
Console.WriteLine("\nMarking node-b unhealthy...");
nodes.Single(n => n.Id == "node-b").MarkUnhealthy();

Console.WriteLine("Round-robin across A, C only (4 requests):");
for (int i = 1; i <= 4; i++)
{
    var response = service.SelectBackend(new SelectBackendRequest());
    Console.WriteLine($"  Request {i} -> {(response.Success ? response.Node!.Address : response.ErrorMessage)}");
}

// --- All nodes unhealthy ---
Console.WriteLine("\nMarking all nodes unhealthy...");
foreach (var n in nodes) n.MarkUnhealthy();

var empty = service.SelectBackend(new SelectBackendRequest());
Console.WriteLine($"Request with no healthy nodes -> {empty.ErrorMessage}");
