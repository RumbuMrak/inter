# Simple LoadBalancer

A small load-balancer library written in **C# / .NET 9**, demonstrating layered architecture, two balancing strategies, periodic health checks, and comprehensive unit tests.

---

## Architecture

The solution follows a strict four-layer dependency rule — each layer depends only on layers below it:

```
LoadBalancer.Domain          (no dependencies)
LoadBalancer.Application     (→ Domain)
LoadBalancer.Infrastructure  (→ Application + Domain)
LoadBalancer.Tests           (→ all three + xUnit + NSubstitute)
LoadBalancer.Sample          (→ all three, console demo)
```

### Layer responsibilities

| Layer | Contents |
|---|---|
| **Domain** | `BackendNode`, `HealthCheckOptions`, `ILoadBalancer`, `IRequestScope` |
| **Application** | DTOs, `ILoadBalancerService`, `INodeHealthChecker`, `IHealthCheckRunner`, `LoadBalancerService`, `HealthCheckOptionsMapper` |
| **Infrastructure** | `RoundRobinLoadBalancer`, `LeastOutstandingRequestsLoadBalancer`, `RequestScope`, `HttpNodeHealthChecker`, `PeriodicHealthCheckRunner` |

---

## Balancing Strategies

### Round Robin (`RoundRobinLoadBalancer`)
Distributes requests evenly in a rotating sequence across all healthy nodes using an atomic `Interlocked.Increment` counter. No per-request state is required.

```
Nodes: A, B, C  →  A, B, C, A, B, C, …
```

### Least Outstanding Requests (`LeastOutstandingRequestsLoadBalancer`)
Selects the healthy node with the fewest currently in-flight requests. Pick + increment is done under a `lock` to prevent two concurrent callers from reading the same minimum before either increments.

Both strategies implement `ILoadBalancer`:

```csharp
public interface ILoadBalancer
{
    BackendNode?    Next();   // stateless — does not track active count
    IRequestScope? Track();  // opens a scope; Dispose() decrements the counter
}
```

---

## Health Checks

`PeriodicHealthCheckRunner` runs a background loop that probes each node's `/health` endpoint (or a configurable path) on a fixed interval.

Key behaviours:
- **Grace period** — newly registered nodes are skipped until `GracePeriod` (default 10 s) has elapsed, giving them time to start.
- **Per-call timeout** — each probe uses a linked `CancellationTokenSource` so a slow node does not block the loop.
- **Fan-out** — all nodes are probed concurrently via `Task.WhenAll`.
- A `200–299` HTTP response marks the node healthy; any error or non-2xx marks it unhealthy.

### Default configuration (`HealthCheckOptions`)

| Property | Default |
|---|---|
| `GracePeriod` | 10 s |
| `Interval` | 15 s |
| `Timeout` | 5 s |
| `HealthPath` | `/health` |

Configuration can be supplied by the caller via `HealthCheckOptionsDto` (any null fields fall back to defaults via `HealthCheckOptionsMapper.ToDomain`).

---

## Request Scope & Active-Request Tracking

`Track()` returns an `IRequestScope`. The scope calls `IncrementActive()` on the selected node in its constructor and `DecrementActive()` on `Dispose()`. `Interlocked.Exchange` guards against double-dispose.

```csharp
using var scope = balancer.Track();
if (scope is null) return ServiceUnavailable();

var node = scope.Node; // guaranteed healthy at selection time
```

---

## Public API (Application layer)

```csharp
// Select a backend (DTO-based, safe for controllers / handlers)
SelectBackendResponse response = loadBalancerService.SelectBackend(new SelectBackendRequest());

// response.Success  — false when no healthy node is available
// response.Node     — BackendNodeDto { Id, Address }
// response.ErrorMessage
```

---

## Getting Started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download)

### Build

```bash
dotnet build LoadBalancer.sln
```

### Run tests

```bash
dotnet test LoadBalancer.Tests/LoadBalancer.Tests.csproj
```

### Run the sample console app

```bash
dotnet run --project LoadBalancer.Sample
```

---

## Tests

All tests use **xUnit 2.9** and **NSubstitute 5.3** for mocking.

| File | Coverage area |
|---|---|
| `LoadBalancerTests.cs` | `LoadBalancerService` + `RoundRobinLoadBalancer` |
| `LeastOutstandingRequestsLoadBalancerTests.cs` | LOR strategy, counter lifecycle, concurrency |
| `HealthCheckTests.cs` | `PeriodicHealthCheckRunner` (grace period, health state, edge cases) |
| `HttpNodeHealthCheckerTests.cs` | `HttpNodeHealthChecker` (HTTP responses, exceptions, URL building) |
| `HealthCheckOptionsMapperTests.cs` | Null/partial/full DTO mapping |
| `InterfaceMockTests.cs` | `ILoadBalancerService`, `IHealthCheckRunner`, `IRequestScope` mock demos |

Edge cases covered: empty node list, all-unhealthy list, null constructor arguments, double-dispose, concurrent acquire/release.

---

## Design Decisions & Trade-offs

| Decision | Rationale |
|---|---|
| Strict layered architecture | Domain stays pure and portable; easy to swap Infrastructure |
| `ILoadBalancer.Track()` alongside `Next()` | Callers that need active-request tracking opt in; simple callers use `Next()` |
| `lock` in LOR (not lock-free) | Correctness over micro-optimisation — the lock scope is tiny and not on the hot path |
| `HealthCheckOptionsMapper` as the single null-resolution point | Infrastructure constructors reject null and fail fast; caller controls override |
| NSubstitute for mocks | Removes hand-rolled fakes, improves maintainability, and verifies call counts declaratively |
