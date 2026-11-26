using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SaveSphere.Modules.Core.Persistence;
using SaveSphere.SharedKernel;
using SaveSphere.TestSupport;

namespace SaveSphere.IntegrationTests;

[Requirement("ARC-02")]
[Requirement("QA-03")]
public sealed class OutboxTests(PostgresFixture postgres)
{
    private sealed record SystemInfoUpdated(string Key, string NewValue) : IDomainEvent
    {
        public string EventType => "core.system-info.updated";
    }

    private static CoreOutbox CreateOutbox(CoreDbContext context) => new(context, TimeProvider.System);

    [Fact]
    public async Task Domain_write_outbox_message_and_event_log_entry_commit_together()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        await using (var context = DatabaseTestHelpers.CreateCoreContext(connection))
        {
            var row = await context.SystemInfo.SingleAsync(x => x.Key == "bootstrap");
            row.Value = "changed";
            CreateOutbox(context).Enqueue(new SystemInfoUpdated("bootstrap", "changed"));
            await context.SaveChangesAsync();
        }

        await using var verify = DatabaseTestHelpers.CreateCoreContext(connection);
        Assert.Equal("changed", (await verify.SystemInfo.SingleAsync()).Value);
        var message = await verify.OutboxMessages.SingleAsync();
        var entry = await verify.EventLog.SingleAsync();
        Assert.Equal("core.system-info.updated", message.EventType);
        Assert.Null(message.ProcessedAt);
        Assert.Equal(0, message.AttemptCount);
        Assert.Equal(message.Payload, entry.Payload);
        Assert.Equal(message.OccurredAt, entry.OccurredAt);
    }

    [Fact]
    public async Task Payload_is_camel_case_json_stored_as_jsonb()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        await using (var context = DatabaseTestHelpers.CreateCoreContext(connection))
        {
            CreateOutbox(context).Enqueue(new SystemInfoUpdated("bootstrap", "x"));
            await context.SaveChangesAsync();
        }

        var newValue = await DatabaseTestHelpers.ScalarAsync<string>(connection, "SELECT payload ->> 'newValue' FROM core.outbox_message");
        var type = await DatabaseTestHelpers.ScalarAsync<string>(connection, "SELECT pg_typeof(payload)::text FROM core.outbox_message");

        Assert.Equal("x", newValue);
        Assert.Equal("jsonb", type);
        using var document = JsonDocument.Parse(await DatabaseTestHelpers.ScalarAsync<string>(connection, "SELECT payload::text FROM core.event_log"));
        Assert.Equal("bootstrap", document.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public async Task Rolling_back_the_transaction_discards_the_domain_write_and_the_events()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        await using (var context = DatabaseTestHelpers.CreateCoreContext(connection))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            var row = await context.SystemInfo.SingleAsync();
            row.Value = "should-not-persist";
            CreateOutbox(context).Enqueue(new SystemInfoUpdated("bootstrap", "should-not-persist"));
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var verify = DatabaseTestHelpers.CreateCoreContext(connection);
        Assert.Equal("e01", (await verify.SystemInfo.SingleAsync()).Value);
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
        Assert.Empty(await verify.EventLog.ToListAsync());
    }

    [Fact]
    public async Task A_failing_event_log_insert_rolls_back_the_domain_write_and_the_outbox_row()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        var existing = Id.New<EventLogEntry>();
        await DatabaseTestHelpers.ExecuteAsync(
            connection,
            $"INSERT INTO core.event_log (id, event_type, payload) VALUES ('{existing}', 'seed', '{{}}')");

        await using (var context = DatabaseTestHelpers.CreateCoreContext(connection))
        {
            var row = await context.SystemInfo.SingleAsync();
            row.Value = "should-not-persist";
            context.OutboxMessages.Add(new OutboxMessage { Id = Id.New<OutboxMessage>(), EventType = "e", Payload = "{}" });
            // Duplicate primary key: the last statement of the unit of work fails.
            context.EventLog.Add(new EventLogEntry { Id = existing, EventType = "e", Payload = "{}" });

            await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using var verify = DatabaseTestHelpers.CreateCoreContext(connection);
        Assert.Equal("e01", (await verify.SystemInfo.SingleAsync()).Value);
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
        Assert.Single(await verify.EventLog.ToListAsync());
    }

    [Theory]
    [InlineData("UPDATE core.event_log SET event_type = 'tampered'")]
    [InlineData("DELETE FROM core.event_log")]
    [InlineData("TRUNCATE core.event_log")]
    public async Task The_event_log_rejects_update_delete_and_truncate(string statement)
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        await DatabaseTestHelpers.ExecuteAsync(
            connection,
            "INSERT INTO core.event_log (event_type, payload) VALUES ('seed', '{}')");

        var failure = await Assert.ThrowsAsync<PostgresException>(() => DatabaseTestHelpers.ExecuteAsync(connection, statement));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, failure.SqlState);
        Assert.Equal(1L, await DatabaseTestHelpers.ScalarAsync<long>(connection, "SELECT count(*) FROM core.event_log"));
        Assert.Equal("seed", await DatabaseTestHelpers.ScalarAsync<string>(connection, "SELECT event_type FROM core.event_log"));
    }

    [Fact]
    public async Task Database_defaults_fill_ids_and_timestamps_for_direct_inserts()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        await DatabaseTestHelpers.ExecuteAsync(connection, "INSERT INTO core.outbox_message (event_type, payload) VALUES ('seed', '{}')");

        Assert.Equal(7, await DatabaseTestHelpers.ScalarAsync<int>(connection, "SELECT uuid_extract_version(id) FROM core.outbox_message"));
        Assert.True(await DatabaseTestHelpers.ScalarAsync<bool>(connection, "SELECT occurred_at IS NOT NULL AND processed_at IS NULL FROM core.outbox_message"));
    }
}
