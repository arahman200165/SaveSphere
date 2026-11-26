namespace SaveSphere.SharedKernel;

/// <summary>
/// Lets the platform operate a module's database without knowing its persistence types:
/// the explicit migrator applies migrations, and the API's readiness check reports pending ones.
/// </summary>
public interface IModuleDatabase
{
    string ModuleName { get; }

    string Schema { get; }

    Task<bool> CanConnectAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken);

    Task MigrateAsync(CancellationToken cancellationToken);
}
