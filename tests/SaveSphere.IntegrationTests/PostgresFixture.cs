using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SaveSphere.Infrastructure.Migrations;
using SaveSphere.Modules.Core;
using SaveSphere.SharedKernel;
using SaveSphere.TestSupport;
using Testcontainers.PostgreSql;
using Xunit;

[assembly: AssemblyFixture(typeof(SaveSphere.IntegrationTests.PostgresFixture))]

namespace SaveSphere.IntegrationTests;

/// <summary>
/// One throwaway PostgreSQL container per test run (ADR-0003). The image tag is read from the pinned
/// docker-compose.yml, so tests and deployment cannot drift. Each test class gets its own database
/// cloned from a migrated template, so tests are isolated and never touch the dev compose database.
/// </summary>
public sealed partial class PostgresFixture : IAsyncLifetime
{
    private const string TemplateName = "savesphere_migrated_template";

    private readonly SemaphoreSlim _templateLock = new(1, 1);
    private readonly List<string> _databases = [];
    private PostgreSqlContainer? _container;
    private bool _templateReady;

    public static string ImageTag { get; } = ReadPinnedImageFromCompose();

    private string AdminConnectionString =>
        (_container ?? throw new InvalidOperationException("Container not started")).GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder(ImageTag)
            .WithPassword(Guid.NewGuid().ToString("N"))
            .Build();
        await _container.StartAsync();
    }

    /// <summary>Creates an isolated database; <paramref name="migrated"/> clones the fully migrated template.</summary>
    public async Task<string> CreateDatabaseAsync(bool migrated)
    {
        if (migrated)
        {
            await EnsureTemplateAsync();
        }

        var name = "t_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(AdminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = migrated
                ? $"CREATE DATABASE \"{name}\" TEMPLATE \"{TemplateName}\""
                : $"CREATE DATABASE \"{name}\"";
            await command.ExecuteNonQueryAsync();
        }

        lock (_databases)
        {
            _databases.Add(name);
        }

        return new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = name }.ConnectionString;
    }

    /// <summary>Applies every module's migrations to the given database through the same code path as the migrator.</summary>
    public static async Task MigrateAsync(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddCoreModule();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await ModuleMigrationRunner.MigrateAllAsync(
            scope.ServiceProvider.GetServices<IModuleDatabase>(),
            NullLogger.Instance,
            CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        _templateLock.Dispose();
    }

    private async Task EnsureTemplateAsync()
    {
        await _templateLock.WaitAsync();
        try
        {
            if (_templateReady)
            {
                return;
            }

            await using (var connection = new NpgsqlConnection(AdminConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE \"{TemplateName}\"";
                await command.ExecuteNonQueryAsync();
            }

            var templateConnection = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = TemplateName }.ConnectionString;
            await MigrateAsync(templateConnection);

            // A template database must have no open connections when it is cloned.
            NpgsqlConnection.ClearAllPools();
            _templateReady = true;
        }
        finally
        {
            _templateLock.Release();
        }
    }

    private static string ReadPinnedImageFromCompose()
    {
        var compose = File.ReadAllText(RepositoryPaths.Combine("docker-compose.yml"));
        var match = PostgresImage().Match(compose);
        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException("docker-compose.yml must pin the postgres image to an exact minor tag, e.g. postgres:18.7");
    }

    [GeneratedRegex(@"^\s*image:\s*(postgres:\d+\.\d+)\s*$", RegexOptions.Multiline)]
    private static partial Regex PostgresImage();
}
