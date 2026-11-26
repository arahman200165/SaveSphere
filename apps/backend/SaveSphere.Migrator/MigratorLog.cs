using Microsoft.Extensions.Logging;

namespace SaveSphere.Migrator;

internal static partial class MigratorLog
{
    [LoggerMessage(Level = LogLevel.Critical, Message = "Migration failed")]
    public static partial void MigrationFailed(ILogger logger, Exception exception);
}
