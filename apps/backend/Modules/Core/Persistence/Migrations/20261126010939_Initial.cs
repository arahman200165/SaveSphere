using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaveSphere.Modules.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            // Reserve every schema defined by the architecture. The names are frozen here on purpose:
            // a migration must not change when PlatformSchemas changes. Each module's own migrations
            // later create objects only inside the schema it owns (ADR-0001).
            foreach (var schema in new[]
            {
                "identity", "productivity", "finance", "health", "knowledge", "inventory",
                "lifestyle", "analytics", "automation", "integrations", "sync", "audit",
            })
            {
                migrationBuilder.Sql($"CREATE SCHEMA IF NOT EXISTS {schema};");
            }

            migrationBuilder.CreateTable(
                name: "event_log",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_message", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_info",
                schema: "core",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_info", x => x.key);
                });

            migrationBuilder.InsertData(
                schema: "core",
                table: "system_info",
                columns: new[] { "key", "updated_at", "value" },
                values: new object[] { "bootstrap", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "e01" });

            migrationBuilder.CreateIndex(
                name: "ix_event_log_occurred_at",
                schema: "core",
                table: "event_log",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_pending",
                schema: "core",
                table: "outbox_message",
                column: "occurred_at",
                filter: "processed_at IS NULL");

            // The event log is append-only (ARC-02): reject UPDATE, DELETE and TRUNCATE at the database.
            migrationBuilder.Sql("""
                CREATE FUNCTION core.forbid_event_log_mutation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'core.event_log is append-only (% not allowed)', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER event_log_append_only
                BEFORE UPDATE OR DELETE ON core.event_log
                FOR EACH ROW EXECUTE FUNCTION core.forbid_event_log_mutation();
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER event_log_no_truncate
                BEFORE TRUNCATE ON core.event_log
                FOR EACH STATEMENT EXECUTE FUNCTION core.forbid_event_log_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS event_log_no_truncate ON core.event_log;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS event_log_append_only ON core.event_log;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS core.forbid_event_log_mutation();");

            migrationBuilder.DropTable(
                name: "event_log",
                schema: "core");

            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "core");

            migrationBuilder.DropTable(
                name: "system_info",
                schema: "core");

            // Restrict semantics: fails (by design) if a later module has already put objects in a schema.
            foreach (var schema in new[]
            {
                "audit", "sync", "integrations", "automation", "analytics", "lifestyle",
                "inventory", "knowledge", "health", "finance", "productivity", "identity",
            })
            {
                migrationBuilder.Sql($"DROP SCHEMA IF EXISTS {schema};");
            }
        }
    }
}
