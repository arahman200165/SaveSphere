namespace SaveSphere.Modules.Core.Persistence;

/// <summary>Tiny key/value table proving the module's schema is reachable and writable.</summary>
public sealed class SystemInfo
{
    public required string Key { get; set; }

    public required string Value { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
