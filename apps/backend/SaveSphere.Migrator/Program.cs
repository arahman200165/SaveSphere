using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SaveSphere.Infrastructure.Logging;
using SaveSphere.Migrator;
using SaveSphere.Infrastructure.Migrations;
using SaveSphere.Modules.Core;
using SaveSphere.SharedKernel;

// Explicit migrator (ADR-0004): the only process that runs DDL.
//   (no args)  apply all pending migrations for every module, exit 0 on success
//   --check    apply nothing; exit 3 if any module has pending migrations
var checkOnly = args.Contains("--check", StringComparer.Ordinal);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog((services, configuration) =>
    configuration
        .ConfigureSaveSphere(services.GetServices<ILogEventSink>())
        // On a brand-new database EF probes the history table before creating it and logs that expected
        // failure as an error. A real migration failure still surfaces as an exception and exit code 1.
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Fatal));
builder.Services.AddCoreModule();

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SaveSphere.Migrator");

try
{
    // Starting the host runs ValidateOnStart, so a missing connection string fails here, before any work.
    await host.StartAsync();

    await using var scope = host.Services.CreateAsyncScope();
    var modules = scope.ServiceProvider.GetServices<IModuleDatabase>().ToList();

    if (checkOnly)
    {
        var pending = await ModuleMigrationRunner.GetPendingAsync(modules, CancellationToken.None);
        return pending.Values.Any(p => p.Count > 0) ? 3 : 0;
    }

    await ModuleMigrationRunner.MigrateAllAsync(modules, logger, CancellationToken.None);
    return 0;
}
#pragma warning disable CA1031 // The process boundary must convert every failure into a non-zero exit code.
catch (Exception ex)
#pragma warning restore CA1031
{
    MigratorLog.MigrationFailed(logger, ex);
    return 1;
}
finally
{
    await host.StopAsync();
}
