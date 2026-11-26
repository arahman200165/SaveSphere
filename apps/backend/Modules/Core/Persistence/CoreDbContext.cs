using Microsoft.EntityFrameworkCore;
using SaveSphere.Infrastructure.Persistence;

namespace SaveSphere.Modules.Core.Persistence;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    public const string Schema = "core";

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<SystemInfo> SystemInfo => Set<SystemInfo>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<EventLogEntry> EventLog => Set<EventLogEntry>();

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
    }
}
