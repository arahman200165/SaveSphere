using Microsoft.EntityFrameworkCore;
using Npgsql;
using SaveSphere.Modules.Core.Persistence;
using SaveSphere.Modules.Core.Records;
using SaveSphere.SharedKernel;
using SaveSphere.TestSupport;

namespace SaveSphere.IntegrationTests;

[Requirement("ARC-01")]
public sealed class RecordRegistryTests(PostgresFixture postgres)
{
    // Marker types standing in for real domain kinds (ARC-01-AC01: transaction, meal, capture).
    private sealed class Transaction;

    private sealed class Meal;

    private sealed class Capture;

    private static readonly RecordKind TransactionKind = RecordKind.Create("finance.transaction");
    private static readonly RecordKind MealKind = RecordKind.Create("lifestyle.meal");
    private static readonly RecordKind CaptureKind = RecordKind.Create("knowledge.capture");
    private static readonly Id<Owner> OwnerId = Id.From<Owner>(Guid.Parse("00000000-0000-7000-8000-000000000001"));
    private static readonly RecordPolicy PrivateHidden = new(Sensitivity.Private, SearchVisibility.Hidden);

    private static RecordRegistration Registration(RecordKind kind, ExternalRef? reference = null, RecordSourceKind source = RecordSourceKind.Manual) =>
        new(kind, OwnerId, source, PrivateHidden, reference);

    private static RecordRegistry CreateRegistry(CoreDbContext context) =>
        new(context, new CoreOutbox(context, TimeProvider.System), TimeProvider.System);

    [Fact]
    [Requirement("ARC-01-AC01")]
    public async Task Transaction_meal_and_capture_have_distinct_typed_ids_and_related_links()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        var transaction = Id.New<Transaction>();
        var meal = Id.New<Meal>();
        var capture = Id.New<Capture>();

        await using (var context = DatabaseTestHelpers.CreateCoreContext(connection))
        {
            var registry = CreateRegistry(context);
            await registry.RegisterAsync(transaction, Registration(TransactionKind));
            await registry.RegisterAsync(meal, Registration(MealKind));
            await registry.RegisterAsync(capture, Registration(CaptureKind, source: RecordSourceKind.Capture));
            await context.SaveChangesAsync();
            await registry.LinkAsync(capture, transaction, "evidence");
            await registry.LinkAsync(meal, transaction, "related");
            await context.SaveChangesAsync();
        }

        await using var verify = DatabaseTestHelpers.CreateCoreContext(connection);
        var kinds = await verify.Records.ToDictionaryAsync(x => x.Id, x => x.Kind);
        Assert.Equal(3, kinds.Count);
        Assert.Equal("finance.transaction", kinds[transaction.Value]);
        Assert.Equal("lifestyle.meal", kinds[meal.Value]);
        Assert.Equal("knowledge.capture", kinds[capture.Value]);
        Assert.Equal(3, new HashSet<Guid> { transaction.Value, meal.Value, capture.Value }.Count);
        Assert.NotEqual(typeof(Id<Transaction>), typeof(Id<Meal>));

        var links = await verify.RecordLinks.OrderBy(x => x.LinkType).ToListAsync();
        Assert.Collection(
            links,
            l => Assert.Equal((capture.Value, transaction.Value, "evidence"), (l.FromRecordId, l.ToRecordId, l.LinkType)),
            l => Assert.Equal((meal.Value, transaction.Value, "related"), (l.FromRecordId, l.ToRecordId, l.LinkType)));
    }

    [Fact]
    [Requirement("ARC-01-AC01")]
    public async Task A_metric_query_counts_each_canonical_expense_once_however_it_is_linked_or_reimported()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        var reference = new ExternalRef("import.bank-csv", "ROW-0001");
        var transaction = Id.New<Transaction>();
        var capture = Id.New<Capture>();
        var secondCapture = Id.New<Capture>();
        var meal = Id.New<Meal>();

        await using (var context = DatabaseTestHelpers.CreateCoreContext(connection))
        {
            var registry = CreateRegistry(context);
            await registry.RegisterAsync(transaction, Registration(TransactionKind, reference, RecordSourceKind.Import));
            await registry.RegisterAsync(capture, Registration(CaptureKind));
            await registry.RegisterAsync(secondCapture, Registration(CaptureKind));
            await registry.RegisterAsync(meal, Registration(MealKind));
            await context.SaveChangesAsync();
            await registry.LinkAsync(capture, transaction, "evidence");
            await registry.LinkAsync(secondCapture, transaction, "evidence");
            await registry.LinkAsync(meal, transaction, "related");
            await context.SaveChangesAsync();

            // The same bank row arrives again (re-import): it resolves to the existing record.
            var again = await registry.RegisterAsync(Id.New<Transaction>(), Registration(TransactionKind, reference, RecordSourceKind.Import));
            await context.SaveChangesAsync();
            Assert.False(again.Created);
            Assert.Equal(transaction, again.Id);
        }

        await using var verify = DatabaseTestHelpers.CreateCoreContext(connection);
        var plain = await verify.Records.CountAsync(x => x.Kind == "finance.transaction" && x.Lifecycle == RecordLifecycle.Active);
        // A metric that follows links must still count the expense once, not once per link.
        var viaLinks = await (
            from record in verify.Records
            join link in verify.RecordLinks on record.Id equals link.ToRecordId
            where record.Kind == "finance.transaction" && record.Lifecycle == RecordLifecycle.Active
            select record.Id).Distinct().CountAsync();
        Assert.Equal(1, plain);
        Assert.Equal(1, viaLinks);
        Assert.Equal(3, await verify.RecordLinks.CountAsync());
        Assert.Equal(1, await verify.RecordExternalRefs.CountAsync());
    }

    [Fact]
    public async Task The_same_external_id_in_different_namespaces_is_two_distinct_records()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        await using var context = DatabaseTestHelpers.CreateCoreContext(connection);
        var registry = CreateRegistry(context);

        var first = await registry.RegisterAsync(Id.New<Transaction>(), Registration(TransactionKind, new ExternalRef("import.bank-a", "1")));
        var second = await registry.RegisterAsync(Id.New<Transaction>(), Registration(TransactionKind, new ExternalRef("import.bank-b", "1")));
        await context.SaveChangesAsync();

        Assert.True(first.Created);
        Assert.True(second.Created);
        Assert.NotEqual(first.Id.Value, second.Id.Value);
        Assert.Equal(first.Id, await registry.FindByExternalRefAsync<Transaction>(new ExternalRef("import.bank-a", "1")));
        Assert.Null(await registry.FindByExternalRefAsync<Transaction>(new ExternalRef("import.bank-c", "1")));
    }

    [Fact]
    public async Task An_external_ref_already_registered_for_another_kind_is_rejected()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        var reference = new ExternalRef("import.mixed", "42");
        await using var context = DatabaseTestHelpers.CreateCoreContext(connection);
        var registry = CreateRegistry(context);
        await registry.RegisterAsync(Id.New<Transaction>(), Registration(TransactionKind, reference));
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.RegisterAsync(Id.New<Meal>(), Registration(MealKind, reference)));
    }

    [Fact]
    public async Task The_database_enforces_one_canonical_record_per_external_ref()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        var first = Id.New<Transaction>();
        var second = Id.New<Transaction>();
        foreach (var id in new[] { first, second })
        {
            await DatabaseTestHelpers.ExecuteAsync(connection, InsertRecord(id.Value, "finance.transaction"));
        }

        await DatabaseTestHelpers.ExecuteAsync(connection, $"INSERT INTO core.record_external_ref (namespace, external_id, record_id) VALUES ('import.bank', '1', '{first}')");
        var failure = await Assert.ThrowsAsync<PostgresException>(() => DatabaseTestHelpers.ExecuteAsync(
            connection,
            $"INSERT INTO core.record_external_ref (namespace, external_id, record_id) VALUES ('import.bank', '1', '{second}')"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
    }

    [Fact]
    public async Task Registration_and_its_event_commit_together_and_roll_back_together()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        var kept = Id.New<Transaction>();
        var discarded = Id.New<Transaction>();

        await using (var context = DatabaseTestHelpers.CreateCoreContext(connection))
        {
            var registry = CreateRegistry(context);
            await registry.RegisterAsync(kept, Registration(TransactionKind, new ExternalRef("import.bank", "SECRET-ROW-ID")));
            await context.SaveChangesAsync();

            await using var transaction = await context.Database.BeginTransactionAsync();
            await registry.RegisterAsync(discarded, Registration(TransactionKind));
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var verify = DatabaseTestHelpers.CreateCoreContext(connection);
        Assert.Equal(kept.Value, (await verify.Records.SingleAsync()).Id);
        var events = await verify.EventLog.ToListAsync();
        var registered = Assert.Single(events);
        Assert.Equal("core.record.registered", registered.EventType);
        Assert.Contains(kept.ToString(), registered.Payload, StringComparison.Ordinal);
        // The event log is permanent: it must not capture the external identifier.
        Assert.DoesNotContain("SECRET-ROW-ID", registered.Payload, StringComparison.Ordinal);
        Assert.Single(await verify.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Policy_is_stored_as_given_and_has_no_database_default_so_a_missing_policy_fails_closed()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        var id = Id.New<Meal>();
        await using (var context = DatabaseTestHelpers.CreateCoreContext(connection))
        {
            await CreateRegistry(context).RegisterAsync(id, new RecordRegistration(MealKind, OwnerId, RecordSourceKind.Manual, new RecordPolicy(Sensitivity.Sensitive, SearchVisibility.Hidden)));
            await context.SaveChangesAsync();
        }

        Assert.Equal("sensitive", await DatabaseTestHelpers.ScalarAsync<string>(connection, "SELECT sensitivity FROM core.record"));
        Assert.Equal("hidden", await DatabaseTestHelpers.ScalarAsync<string>(connection, "SELECT search_visibility FROM core.record"));

        var failure = await Assert.ThrowsAsync<PostgresException>(() => DatabaseTestHelpers.ExecuteAsync(
            connection,
            $"INSERT INTO core.record (id, kind, owner_id, source, lifecycle) VALUES ('{Id.New<Meal>()}', 'lifestyle.meal', '{OwnerId}', 'manual', 'active')"));
        Assert.Equal(PostgresErrorCodes.NotNullViolation, failure.SqlState);
    }

    [Fact]
    public async Task Registration_requires_an_explicit_policy()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        await using var context = DatabaseTestHelpers.CreateCoreContext(connection);

        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateRegistry(context).RegisterAsync(
            Id.New<Meal>(),
            new RecordRegistration(MealKind, OwnerId, RecordSourceKind.Manual, null!)));
    }

    [Theory]
    [InlineData("kind", "'Finance.Transaction'")]
    [InlineData("kind", "'transaction'")]
    [InlineData("source", "'telepathy'")]
    [InlineData("lifecycle", "'zombie'")]
    [InlineData("sensitivity", "'secret'")]
    [InlineData("search_visibility", "'maybe'")]
    public async Task The_database_rejects_values_outside_the_allowed_sets(string column, string value)
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        var values = new Dictionary<string, string>
        {
            ["kind"] = "'finance.transaction'",
            ["source"] = "'manual'",
            ["lifecycle"] = "'active'",
            ["sensitivity"] = "'private'",
            ["search_visibility"] = "'hidden'",
        };
        values[column] = value;

        var failure = await Assert.ThrowsAsync<PostgresException>(() => DatabaseTestHelpers.ExecuteAsync(
            connection,
            $"INSERT INTO core.record (id, kind, owner_id, source, lifecycle, sensitivity, search_visibility) VALUES ('{Id.New<Transaction>()}', {values["kind"]}, '{OwnerId}', {values["source"]}, {values["lifecycle"]}, {values["sensitivity"]}, {values["search_visibility"]})"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
    }

    [Theory]
    [InlineData("kind = 'lifestyle.meal'")]
    [InlineData("owner_id = '00000000-0000-7000-8000-0000000000ff'")]
    [InlineData("source = 'import'")]
    [InlineData("created_at = created_at - interval '1 day'")]
    [InlineData("id = '00000000-0000-7000-8000-0000000000aa'")]
    public async Task A_records_identity_columns_cannot_be_changed(string assignment)
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        await DatabaseTestHelpers.ExecuteAsync(connection, InsertRecord(Id.New<Transaction>().Value, "finance.transaction"));

        var failure = await Assert.ThrowsAsync<PostgresException>(() => DatabaseTestHelpers.ExecuteAsync(connection, $"UPDATE core.record SET {assignment}"));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, failure.SqlState);
    }

    [Fact]
    public async Task Archiving_and_restoring_work_but_deletion_is_a_terminal_tombstone()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        var id = Id.New<Transaction>();
        await using (var context = DatabaseTestHelpers.CreateCoreContext(connection))
        {
            var registry = CreateRegistry(context);
            await registry.RegisterAsync(id, Registration(TransactionKind));
            await context.SaveChangesAsync();
            await registry.SetLifecycleAsync(id, RecordLifecycle.Archived);
            await context.SaveChangesAsync();
            await registry.SetLifecycleAsync(id, RecordLifecycle.Active);
            await context.SaveChangesAsync();
            await registry.SetLifecycleAsync(id, RecordLifecycle.Deleted);
            await context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => registry.SetLifecycleAsync(id, RecordLifecycle.Active));
        }

        Assert.Equal("deleted", await DatabaseTestHelpers.ScalarAsync<string>(connection, "SELECT lifecycle FROM core.record"));
        var failure = await Assert.ThrowsAsync<PostgresException>(() => DatabaseTestHelpers.ExecuteAsync(connection, "UPDATE core.record SET lifecycle = 'active'"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, failure.SqlState);
        Assert.Equal(
            ["core.record.lifecycle-changed", "core.record.lifecycle-changed", "core.record.lifecycle-changed", "core.record.registered"],
            await DatabaseTestHelpers.StringsAsync(connection, "SELECT event_type FROM core.event_log ORDER BY event_type"));
    }

    [Fact]
    public async Task Linking_is_idempotent_validated_and_requires_registered_records()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        var transaction = Id.New<Transaction>();
        var capture = Id.New<Capture>();
        await using var context = DatabaseTestHelpers.CreateCoreContext(connection);
        var registry = CreateRegistry(context);
        await registry.RegisterAsync(transaction, Registration(TransactionKind));
        await registry.RegisterAsync(capture, Registration(CaptureKind));
        await context.SaveChangesAsync();

        await registry.LinkAsync(capture, transaction, "evidence");
        await context.SaveChangesAsync();
        await registry.LinkAsync(capture, transaction, "evidence");
        await context.SaveChangesAsync();

        Assert.Equal(1, await context.RecordLinks.CountAsync());
        Assert.Equal(1, await context.EventLog.CountAsync(x => x.EventType == "core.record.linked"));
        await Assert.ThrowsAsync<ArgumentException>(() => registry.LinkAsync(capture, transaction, "Not Kebab"));
        await Assert.ThrowsAsync<ArgumentException>(() => registry.LinkAsync(transaction, transaction, "evidence"));

        // The link target must be a registered record: the foreign key rejects a dangling link.
        await registry.LinkAsync(capture, Id.New<Meal>(), "related");
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Records_with_links_cannot_be_hard_deleted()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        var transaction = Id.New<Transaction>();
        var capture = Id.New<Capture>();
        await using (var context = DatabaseTestHelpers.CreateCoreContext(connection))
        {
            var registry = CreateRegistry(context);
            await registry.RegisterAsync(transaction, Registration(TransactionKind));
            await registry.RegisterAsync(capture, Registration(CaptureKind));
            await context.SaveChangesAsync();
            await registry.LinkAsync(capture, transaction, "evidence");
            await context.SaveChangesAsync();
        }

        var failure = await Assert.ThrowsAsync<PostgresException>(() => DatabaseTestHelpers.ExecuteAsync(connection, $"DELETE FROM core.record WHERE id = '{transaction}'"));

        // ON DELETE RESTRICT reports restrict_violation, not foreign_key_violation.
        Assert.Equal(PostgresErrorCodes.RestrictViolation, failure.SqlState);
        Assert.Equal(2L, await DatabaseTestHelpers.ScalarAsync<long>(connection, "SELECT count(*) FROM core.record"));
    }

    private static string InsertRecord(Guid id, string kind) =>
        $"INSERT INTO core.record (id, kind, owner_id, source, lifecycle, sensitivity, search_visibility) VALUES ('{id}', '{kind}', '{OwnerId}', 'manual', 'active', 'private', 'hidden')";
}
