using SaveSphere.SharedKernel;

namespace SaveSphere.Modules.Core.Persistence;

/// <summary>
/// A typed event awaiting delivery. Written in the same transaction as the domain change that caused it.
/// The dispatcher that drains it arrives with the worker (ARC-09, phase 06).
/// </summary>
public sealed class OutboxMessage
{
    public Id<OutboxMessage> Id { get; set; }

    public required string EventType { get; set; }

    /// <summary>JSON payload stored as jsonb.</summary>
    public required string Payload { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}
