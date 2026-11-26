using SaveSphere.SharedKernel;

namespace SaveSphere.Modules.Core.Records;

// Event payloads are permanent (the event log is append-only), so they carry identifiers and kinds only:
// never external IDs or any field of the domain row.

public sealed record RecordRegistered(Guid RecordId, string Kind) : IDomainEvent
{
    public string EventType => "core.record.registered";
}

public sealed record RecordLifecycleChanged(Guid RecordId, string Kind, string From, string To) : IDomainEvent
{
    public string EventType => "core.record.lifecycle-changed";
}

public sealed record RecordsLinked(Guid FromRecordId, Guid ToRecordId, string LinkType) : IDomainEvent
{
    public string EventType => "core.record.linked";
}
