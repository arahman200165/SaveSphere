using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SaveSphere.Infrastructure.Persistence;
using SaveSphere.SharedKernel;

namespace SaveSphere.Modules.Core.Persistence;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    public const string Schema = "core";

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<SystemInfo> SystemInfo => Set<SystemInfo>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<EventLogEntry> EventLog => Set<EventLogEntry>();

    public DbSet<RegisteredRecord> Records => Set<RegisteredRecord>();

    public DbSet<RecordLink> RecordLinks => Set<RecordLink>();

    public DbSet<RecordExternalRef> RecordExternalRefs => Set<RecordExternalRef>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<SystemInfo>(b =>
        {
            b.ToTable("system_info");
            b.HasKey(x => x.Key);
            b.Property(x => x.Key).HasMaxLength(100);
            b.Property(x => x.Value).HasMaxLength(500);
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            b.HasData(new SystemInfo
            {
                Key = "bootstrap",
                Value = "e01",
                UpdatedAt = new DateTimeOffset(2026, 11, 25, 0, 0, 0, TimeSpan.Zero),
            });
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_message");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .HasConversion(new IdValueConverter<OutboxMessage>())
                .HasDefaultValueSql("uuidv7()")
                .ValueGeneratedNever();
            b.Property(x => x.EventType).HasMaxLength(200);
            b.Property(x => x.Payload).HasColumnType("jsonb");
            b.Property(x => x.OccurredAt).HasDefaultValueSql("now()");
            b.Property(x => x.AttemptCount).HasDefaultValue(0);
            b.HasIndex(x => x.OccurredAt).HasFilter("processed_at IS NULL").HasDatabaseName("ix_outbox_message_pending");
        });

        modelBuilder.Entity<EventLogEntry>(b =>
        {
            b.ToTable("event_log");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .HasConversion(new IdValueConverter<EventLogEntry>())
                .HasDefaultValueSql("uuidv7()")
                .ValueGeneratedNever();
            b.Property(x => x.EventType).HasMaxLength(200);
            b.Property(x => x.Payload).HasColumnType("jsonb");
            b.Property(x => x.OccurredAt).HasDefaultValueSql("now()");
            b.Property(x => x.RecordedAt).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            b.HasIndex(x => x.OccurredAt).HasDatabaseName("ix_event_log_occurred_at");
        });

        modelBuilder.Entity<RegisteredRecord>(b =>
        {
            b.ToTable("record", t =>
            {
                t.HasCheckConstraint("ck_record_kind", $"kind ~ '{RecordKind.Pattern}'");
                t.HasCheckConstraint("ck_record_source", "source IN ('manual', 'capture', 'import', 'connector', 'derived')");
                t.HasCheckConstraint("ck_record_lifecycle", "lifecycle IN ('active', 'archived', 'deleted')");
                t.HasCheckConstraint("ck_record_sensitivity", "sensitivity IN ('standard', 'private', 'sensitive')");
                t.HasCheckConstraint("ck_record_search_visibility", "search_visibility IN ('visible', 'hidden')");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Kind).HasMaxLength(100);
            b.Property(x => x.Source).HasConversion(LowerCase<RecordSourceKind>()).HasMaxLength(20);
            b.Property(x => x.Lifecycle).HasConversion(LowerCase<RecordLifecycle>()).HasMaxLength(20);
            b.Property(x => x.Sensitivity).HasConversion(LowerCase<Sensitivity>()).HasMaxLength(20);
            b.Property(x => x.SearchVisibility).HasConversion(LowerCase<SearchVisibility>()).HasMaxLength(20);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            b.HasIndex(x => new { x.Kind, x.Lifecycle }).HasDatabaseName("ix_record_kind_lifecycle");
        });

        modelBuilder.Entity<RecordLink>(b =>
        {
            b.ToTable("record_link", t =>
            {
                t.HasCheckConstraint("ck_record_link_not_self", "from_record_id <> to_record_id");
                t.HasCheckConstraint("ck_record_link_type", $"link_type ~ '{SaveSphere.Modules.Core.Records.RecordRegistry.LinkTypePattern}'");
            });
            b.HasKey(x => new { x.FromRecordId, x.ToRecordId, x.LinkType });
            b.Property(x => x.LinkType).HasMaxLength(100);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            b.HasOne<RegisteredRecord>().WithMany().HasForeignKey(x => x.FromRecordId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RegisteredRecord>().WithMany().HasForeignKey(x => x.ToRecordId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.ToRecordId).HasDatabaseName("ix_record_link_to_record_id");
        });

        modelBuilder.Entity<RecordExternalRef>(b =>
        {
            b.ToTable("record_external_ref", t =>
                t.HasCheckConstraint("ck_record_external_ref_namespace", $"namespace ~ '{ExternalRef.NamespacePattern}'"));
            b.HasKey(x => new { x.Namespace, x.ExternalId });
            b.Property(x => x.Namespace).HasMaxLength(100);
            b.Property(x => x.ExternalId).HasMaxLength(ExternalRef.MaxExternalIdLength);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            b.HasOne<RegisteredRecord>().WithMany().HasForeignKey(x => x.RecordId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.RecordId).HasDatabaseName("ix_record_external_ref_record_id");
        });
    }

    private static ValueConverter<T, string> LowerCase<T>()
        where T : struct, Enum =>
        new(v => v.ToString().ToLowerInvariant(), s => Enum.Parse<T>(s, true));
}
