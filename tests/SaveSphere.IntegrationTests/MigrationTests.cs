using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SaveSphere.SharedKernel;
using SaveSphere.TestSupport;

namespace SaveSphere.IntegrationTests;

[Requirement("ARC-12")]
[Requirement("NFR-07")]
public sealed class MigrationTests(PostgresFixture postgres)
{
    private const string SchemaQuery =
        "SELECT schema_name FROM information_schema.schemata WHERE schema_name NOT LIKE 'pg\\_%' AND schema_name <> 'information_schema'";

    [Fact]
    public void The_postgres_image_is_pinned_to_an_exact_minor_tag()
    {
        Assert.Matches(@"^postgres:18\.\d+$", PostgresFixture.ImageTag);
    }

    [Fact]
    public async Task Migrations_apply_cleanly_to_an_empty_database()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: false);

        await PostgresFixture.MigrateAsync(connection);

        await using var context = DatabaseTestHelpers.CreateCoreContext(connection);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Applying_migrations_a_second_time_changes_nothing()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: false);
        await PostgresFixture.MigrateAsync(connection);
        var before = await DatabaseTestHelpers.StringsAsync(connection, "SELECT migration_id FROM core.__ef_migrations_history ORDER BY 1");

        await PostgresFixture.MigrateAsync(connection);

        var after = await DatabaseTestHelpers.StringsAsync(connection, "SELECT migration_id FROM core.__ef_migrations_history ORDER BY 1");
        Assert.Equal(before, after);
        Assert.Equal(1L, await DatabaseTestHelpers.ScalarAsync<long>(connection, "SELECT count(*) FROM core.system_info"));
    }

    [Fact]
    public async Task All_thirteen_architecture_schemas_exist()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);

        var schemas = await DatabaseTestHelpers.StringsAsync(connection, SchemaQuery);

        foreach (var expected in PlatformSchemas.All)
        {
            Assert.Contains(expected, schemas);
        }
    }

    [Fact]
    public async Task Only_core_has_tables_and_everything_else_is_a_reserved_empty_schema()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);

        var schemaList = string.Join(",", PlatformSchemas.All.Select(s => $"'{s}'"));
        var tablesBySchema = await DatabaseTestHelpers.StringsAsync(
            connection,
            $"SELECT table_schema || '.' || table_name FROM information_schema.tables WHERE table_schema IN ({schemaList})");

        Assert.All(tablesBySchema, t => Assert.StartsWith("core.", t, StringComparison.Ordinal));
        Assert.Equivalent(
            new[] { "core.__ef_migrations_history", "core.event_log", "core.outbox_message", "core.system_info" },
            tablesBySchema);
    }

    [Fact]
    public async Task Migration_history_lives_in_the_module_schema_not_in_public()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);

        var historyTables = await DatabaseTestHelpers.StringsAsync(
            connection,
            "SELECT table_schema FROM information_schema.tables WHERE table_name = '__ef_migrations_history'");

        Assert.Equal(["core"], historyTables);
        Assert.Empty(await DatabaseTestHelpers.StringsAsync(
            connection,
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"));
    }

    [Fact]
    public async Task Postgres_generates_time_ordered_uuid_version_7_values()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);

        var version = await DatabaseTestHelpers.ScalarAsync<int>(connection, "SELECT uuid_extract_version(uuidv7())");

        Assert.Equal(7, version);
    }

    [Fact]
    public async Task The_core_system_info_row_is_seeded()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        await using var context = DatabaseTestHelpers.CreateCoreContext(connection);

        var row = await context.SystemInfo.SingleAsync(x => x.Key == "bootstrap");

        Assert.Equal("e01", row.Value);
    }

    [Fact]
    public async Task Rolling_back_to_zero_removes_everything_and_the_migration_can_be_reapplied()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        await using (var context = DatabaseTestHelpers.CreateCoreContext(connection))
        {
            await context.GetService<IMigrator>().MigrateAsync("0");
        }

        var remainingTables = await DatabaseTestHelpers.StringsAsync(
            connection,
            "SELECT table_schema || '.' || table_name FROM information_schema.tables WHERE table_schema = 'core' AND table_name <> '__ef_migrations_history'");
        var schemas = await DatabaseTestHelpers.StringsAsync(connection, SchemaQuery);

        Assert.Empty(remainingTables);
        Assert.Equal(0L, await DatabaseTestHelpers.ScalarAsync<long>(connection, "SELECT count(*) FROM pg_proc WHERE proname = 'forbid_event_log_mutation'"));
        Assert.DoesNotContain("finance", schemas);
        Assert.DoesNotContain("audit", schemas);

        await PostgresFixture.MigrateAsync(connection);

        Assert.Contains("finance", await DatabaseTestHelpers.StringsAsync(connection, SchemaQuery));
        Assert.Equal(1L, await DatabaseTestHelpers.ScalarAsync<long>(connection, "SELECT count(*) FROM core.system_info"));
    }
}
