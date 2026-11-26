using Microsoft.Extensions.Logging;
using SaveSphere.SharedKernel;

namespace SaveSphere.Infrastructure.Migrations;

/// <summary>
/// Applies (or reports) migrations for every registered module. This is the only code path that runs DDL;
/// the API never does (ADR-0004).
/// </summary>
public static partial class ModuleMigrationRunner
{
    public static async Task MigrateAllAsync(
        IEnumerable<IModuleDatabase> modules,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(logger);

        foreach (var module in modules)
        {
            var pending = await module.GetPendingMigrationsAsync(cancellationToken);
            LogApplying(logger, module.ModuleName, pending.Count);
            await module.MigrateAsync(cancellationToken);
            LogApplied(logger, module.ModuleName);
        }
    }

    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetPendingAsync(
        IEnumerable<IModuleDatabase> modules,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            result[module.ModuleName] = await module.GetPendingMigrationsAsync(cancellationToken);
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Module {Module}: applying {PendingCount} pending migration(s)")]
    private static partial void LogApplying(ILogger logger, string module, int pendingCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Module {Module}: database is up to date")]
    private static partial void LogApplied(ILogger logger, string module);
}
