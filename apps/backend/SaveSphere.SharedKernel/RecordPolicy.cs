namespace SaveSphere.SharedKernel;

/// <summary>Sensitivity values from SEC-01. "Locked" is an access state, not a level; local-only is a storage constraint.</summary>
public enum Sensitivity
{
    Standard,
    Private,
    Sensitive,
}

/// <summary>Whether a record may appear in search results. An independent SEC-01 dimension, never inferred from sensitivity.</summary>
public enum SearchVisibility
{
    Visible,
    Hidden,
}

/// <summary>Lifecycle of the canonical identity. <c>Deleted</c> is a tombstone: the ID is never reused (ARC-02).</summary>
public enum RecordLifecycle
{
    Active,
    Archived,
    Deleted,
}

/// <summary>How a record entered the system. Provenance only; it grants no trust.</summary>
public enum RecordSourceKind
{
    Manual,
    Capture,
    Import,
    Connector,
    Derived,
}

/// <summary>
/// Privacy and visibility policy supplied at registration. There is deliberately no default: a caller that has
/// not decided cannot register a record (SEC-04, missing policy fails closed).
/// </summary>
public sealed record RecordPolicy(Sensitivity Sensitivity, SearchVisibility SearchVisibility);
