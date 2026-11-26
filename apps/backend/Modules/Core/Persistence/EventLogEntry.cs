using SaveSphere.SharedKernel;

namespace SaveSphere.Modules.Core.Persistence;

/// <summary>Append-only history of domain events. A database trigger rejects UPDATE, DELETE and TRUNCATE.</summary>
public sealed class EventLogEntry
{
    public Id<EventLogEntry> Id { get; set; }

    public required string EventType { get; set; }

    public required string Payload { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset RecordedAt { get; set; }
}
