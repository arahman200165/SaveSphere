using Microsoft.EntityFrameworkCore;
using SaveSphere.SharedKernel;

namespace SaveSphere.Modules.Core.Persistence;

internal sealed class CoreModuleDatabase(CoreDbContext context) : IModuleDatabase
{
    public string ModuleName => "core";

    public string Schema => CoreDbContext.Schema;

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        context.Database.CanConnectAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken) =>
        (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

    public Task MigrateAsync(CancellationToken cancellationToken) =>
        context.Database.MigrateAsync(cancellationToken);
}
