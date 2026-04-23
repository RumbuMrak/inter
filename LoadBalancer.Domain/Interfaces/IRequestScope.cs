using LoadBalancer.Domain.Entities;

namespace LoadBalancer.Domain.Interfaces;

/// <summary>
/// Represents a single in-flight request bound to a <see cref="BackendNode"/>.
/// Disposing this scope signals that the request has completed and decrements
/// the node's <see cref="BackendNode.ActiveRequests"/> counter.
/// </summary>
public interface IRequestScope : IDisposable
{
    BackendNode Node { get; }
}
