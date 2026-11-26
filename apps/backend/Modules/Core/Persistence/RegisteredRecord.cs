using SaveSphere.SharedKernel;

namespace SaveSphere.Modules.Core.Persistence;

/// <summary>
/// ARC-01 thin shared identity (<c>core.record</c>). The primary key is the same value as the primary key of the
/// domain row that owns the facts. There is no payload column on purpose: concrete domain tables own the data.
/// </summary>
public sealed class RegisteredRecord
{
    public Guid Id { get; set; }

    public required string Kind { get; set; }

    public Guid OwnerId { get; set; }

    public RecordSourceKind Source { get; set; }

    public RecordLifecycle Lifecycle { get; set; }

    public Sensitivity Sensitivity { get; set; }

    public SearchVisibility SearchVisibility { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A directional, typed link between two registered records (<c>core.record_link</c>).</summary>
public sealed class RecordLink
{
    public Guid FromRecordId { get; set; }

    public Guid ToRecordId { get; set; }

    public required string LinkType { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Maps an outside system's identifier to the canonical record (<c>core.record_external_ref</c>).</summary>
public sealed class RecordExternalRef
{
    public required string Namespace { get; set; }

    public required string ExternalId { get; set; }

    public Guid RecordId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
