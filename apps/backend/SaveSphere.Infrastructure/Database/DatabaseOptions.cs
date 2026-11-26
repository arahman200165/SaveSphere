using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;

namespace SaveSphere.Infrastructure.Database;

/// <summary>
/// Database connection settings. There is deliberately no default: the value comes from
/// environment or a secret file outside the repo, and startup fails if it is missing.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required(AllowEmptyStrings = false)]
    public string Default { get; set; } = string.Empty;
}

public static class DatabaseOptionsExtensions
{
    public static IServiceCollection AddSaveSphereDatabaseOptions(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }
}
