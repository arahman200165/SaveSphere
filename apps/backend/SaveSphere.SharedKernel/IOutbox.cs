namespace SaveSphere.SharedKernel;

/// <summary>
/// Transactional outbox. <see cref="Enqueue"/> stages the event in the caller's current unit of work,
/// so the event commits or rolls back together with the domain write that caused it.
/// </summary>
public interface IOutbox
{
    void Enqueue(IDomainEvent domainEvent);
}
