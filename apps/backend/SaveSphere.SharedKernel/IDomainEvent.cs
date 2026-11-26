namespace SaveSphere.SharedKernel;

/// <summary>
/// A typed fact that happened inside a module. Cross-module interaction uses these events
/// through <see cref="IOutbox"/>, never direct table writes (ARC-01, ARC-02).
/// </summary>
public interface IDomainEvent
{
    /// <summary>Stable, namespaced event name, e.g. <c>core.system-info.updated</c>.</summary>
    string EventType { get; }
}
