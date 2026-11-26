using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SaveSphere.Infrastructure.Database;
using SaveSphere.Modules.Core.Persistence;
using SaveSphere.SharedKernel;

namespace SaveSphere.Modules.Core;

public static class CoreModule
{
    public static IServiceCollection AddCoreModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSaveSphereDatabaseOptions();

        services.AddDbContext<CoreDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.Default;
            options
                .UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable(CoreDbContext.MigrationsHistoryTable, CoreDbContext.Schema))
                .UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IOutbox, CoreOutbox>();
        services.AddScoped<IModuleDatabase, CoreModuleDatabase>();
        return services;
    }
}
