using Microsoft.EntityFrameworkCore;
using Npgsql;
using SaveSphere.Modules.Core.Persistence;

namespace SaveSphere.IntegrationTests;

internal static class DatabaseTestHelpers
{
    public static CoreDbContext CreateCoreContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(CoreDbContext.MigrationsHistoryTable, CoreDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new CoreDbContext(options);
    }

    public static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    public static async Task<List<string>> StringsAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
