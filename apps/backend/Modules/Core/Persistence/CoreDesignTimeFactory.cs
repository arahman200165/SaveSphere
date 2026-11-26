using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SaveSphere.Modules.Core.Persistence;

/// <summary>
/// Used only by "dotnet ef migrations add", which builds the model without connecting.
/// The connection string is a credential-free placeholder and is never used at runtime.
/// </summary>
internal sealed class CoreDesignTimeFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time_only", npgsql =>
                npgsql.MigrationsHistoryTable(CoreDbContext.MigrationsHistoryTable, CoreDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new CoreDbContext(options);
    }
}
