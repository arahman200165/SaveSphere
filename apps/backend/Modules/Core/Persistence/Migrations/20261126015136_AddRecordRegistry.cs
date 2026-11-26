using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaveSphere.Modules.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "record",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lifecycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sensitivity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    search_visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_record", x => x.id);
                    table.CheckConstraint("ck_record_kind", "kind ~ '^[a-z][a-z0-9]*(-[a-z0-9]+)*\\.[a-z][a-z0-9]*(-[a-z0-9]+)*$'");
                    table.CheckConstraint("ck_record_lifecycle", "lifecycle IN ('active', 'archived', 'deleted')");
                    table.CheckConstraint("ck_record_search_visibility", "search_visibility IN ('visible', 'hidden')");
                    table.CheckConstraint("ck_record_sensitivity", "sensitivity IN ('standard', 'private', 'sensitive')");
                    table.CheckConstraint("ck_record_source", "source IN ('manual', 'capture', 'import', 'connector', 'derived')");
                });

            migrationBuilder.CreateTable(
                name: "record_external_ref",
                schema: "core",
                columns: table => new
                {
                    @namespace = table.Column<string>(name: "namespace", type: "character varying(100)", maxLength: 100, nullable: false),
                    external_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_record_external_ref", x => new { x.@namespace, x.external_id });
                    table.CheckConstraint("ck_record_external_ref_namespace", "namespace ~ '^[a-z][a-z0-9]*([.-][a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_record_external_ref_record_record_id",
                        column: x => x.record_id,
                        principalSchema: "core",
                        principalTable: "record",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "record_link",
                schema: "core",
                columns: table => new
                {
                    from_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_record_link", x => new { x.from_record_id, x.to_record_id, x.link_type });
                    table.CheckConstraint("ck_record_link_not_self", "from_record_id <> to_record_id");
                    table.CheckConstraint("ck_record_link_type", "link_type ~ '^[a-z][a-z0-9]*(-[a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_record_link_record_from_record_id",
                        column: x => x.from_record_id,
                        principalSchema: "core",
                        principalTable: "record",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_record_link_record_to_record_id",
                        column: x => x.to_record_id,
                        principalSchema: "core",
                        principalTable: "record",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_record_kind_lifecycle",
                schema: "core",
                table: "record",
                columns: new[] { "kind", "lifecycle" });

            migrationBuilder.CreateIndex(
                name: "ix_record_external_ref_record_id",
                schema: "core",
                table: "record_external_ref",
                column: "record_id");

            migrationBuilder.CreateIndex(
                name: "ix_record_link_to_record_id",
                schema: "core",
                table: "record_link",
                column: "to_record_id");

            // Identity of a record never changes, and a deleted record is a tombstone (ARC-01, ARC-02).
            migrationBuilder.Sql("""
                CREATE FUNCTION core.guard_record_update() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.id IS DISTINCT FROM OLD.id
                        OR NEW.kind IS DISTINCT FROM OLD.kind
                        OR NEW.owner_id IS DISTINCT FROM OLD.owner_id
                        OR NEW.source IS DISTINCT FROM OLD.source
                        OR NEW.created_at IS DISTINCT FROM OLD.created_at THEN
                        RAISE EXCEPTION 'core.record identity columns are immutable'
                            USING ERRCODE = 'restrict_violation';
                    END IF;
                    IF OLD.lifecycle = 'deleted' AND NEW.lifecycle IS DISTINCT FROM OLD.lifecycle THEN
                        RAISE EXCEPTION 'a deleted core.record is a tombstone and cannot change lifecycle'
                            USING ERRCODE = 'restrict_violation';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER record_guard_update
                BEFORE UPDATE ON core.record
                FOR EACH ROW EXECUTE FUNCTION core.guard_record_update();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS record_guard_update ON core.record;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS core.guard_record_update();");

            migrationBuilder.DropTable(
                name: "record_external_ref",
                schema: "core");

            migrationBuilder.DropTable(
                name: "record_link",
                schema: "core");

            migrationBuilder.DropTable(
                name: "record",
                schema: "core");
        }
    }
}
