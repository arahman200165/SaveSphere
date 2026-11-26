using System.Collections.ObjectModel;

namespace SaveSphere.SharedKernel;

/// <summary>
/// The PostgreSQL schemas defined by the architecture (CLAUDE.md "Fixed architecture").
/// Each module owns exactly one; nothing writes to another module's schema.
/// </summary>
public static class PlatformSchemas
{
    public static IReadOnlyList<string> All { get; } = new ReadOnlyCollection<string>(
    [
        "identity",
        "core",
        "productivity",
        "finance",
        "health",
        "knowledge",
        "inventory",
        "lifestyle",
        "analytics",
        "automation",
        "integrations",
        "sync",
        "audit",
    ]);
}
