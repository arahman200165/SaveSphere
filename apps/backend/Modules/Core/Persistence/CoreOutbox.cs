using System.Text.Json;
using SaveSphere.SharedKernel;

namespace SaveSphere.Modules.Core.Persistence;

/// <summary>
/// Stages an outbox message and an event-log entry on the caller's <see cref="CoreDbContext"/>.
/// They are persisted by the same SaveChanges (one database transaction) as the domain write.
/// </summary>
public sealed class CoreOutbox(CoreDbContext context, TimeProvider clock) : IOutbox
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public void Enqueue(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), PayloadOptions);
        var occurredAt = clock.GetUtcNow();

        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = Id.New<OutboxMessage>(),
            EventType = domainEvent.EventType,
            Payload = payload,
            OccurredAt = occurredAt,
        });
        context.EventLog.Add(new EventLogEntry
        {
            Id = Id.New<EventLogEntry>(),
            EventType = domainEvent.EventType,
            Payload = payload,
            OccurredAt = occurredAt,
        });
    }
}
