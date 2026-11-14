using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feature_flags",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    default_value = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    added_revision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_revision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    listed_count = table.Column<int>(type: "integer", nullable: false),
                    percentage = table.Column<int>(type: "integer", nullable: false),
                    everyone_else = table.Column<bool>(type: "boolean", nullable: false),
                    state_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_changed_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature_flags", x => x.id);
                    table.CheckConstraint("ck_feature_flags_default_value", "default_value = (type = 'KillSwitch')");
                    table.CheckConstraint("ck_feature_flags_listed_count", "listed_count >= 0");
                    table.CheckConstraint("ck_feature_flags_percentage", "percentage BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_feature_flags_removal", "removed_at IS NOT NULL OR removed_revision IS NULL");
                    table.CheckConstraint("ck_feature_flags_type", "type IN ('Release', 'KillSwitch')");
                    table.ForeignKey(
                        name: "fk_feature_flags_users_last_changed_by",
                        column: x => x.last_changed_by,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "feature_flag_changes",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    from = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    to = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature_flag_changes", x => x.id);
                    table.CheckConstraint("ck_feature_flag_changes_actor", "(kind IN ('AddedToCode', 'RemovedFromCode')) = (actor_id IS NULL)");
                    table.CheckConstraint("ck_feature_flag_changes_kind", "kind IN ('AddedToCode', 'RemovedFromCode', 'Listed', 'Unlisted', 'PercentageChanged', 'EveryoneElseChanged')");
                    table.CheckConstraint("ck_feature_flag_changes_tenant", "(kind IN ('Listed', 'Unlisted')) = (tenant_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_feature_flag_changes_feature_flags_flag_id",
                        column: x => x.flag_id,
                        principalSchema: "control_plane",
                        principalTable: "feature_flags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_feature_flag_changes_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "control_plane",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_feature_flag_changes_users_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "feature_flag_tenants",
                schema: "control_plane",
                columns: table => new
                {
                    flag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    listed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    listed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature_flag_tenants", x => new { x.flag_id, x.tenant_id });
                    table.ForeignKey(
                        name: "fk_feature_flag_tenants_feature_flags_flag_id",
                        column: x => x.flag_id,
                        principalSchema: "control_plane",
                        principalTable: "feature_flags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_feature_flag_tenants_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "control_plane",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_feature_flag_tenants_users_listed_by",
                        column: x => x.listed_by,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_feature_flag_changes_actor_id",
                schema: "control_plane",
                table: "feature_flag_changes",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_feature_flag_changes_flag_id_at",
                schema: "control_plane",
                table: "feature_flag_changes",
                columns: new[] { "flag_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_feature_flag_changes_tenant_id",
                schema: "control_plane",
                table: "feature_flag_changes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_feature_flag_tenants_listed_by",
                schema: "control_plane",
                table: "feature_flag_tenants",
                column: "listed_by");

            migrationBuilder.CreateIndex(
                name: "ix_feature_flag_tenants_tenant_id",
                schema: "control_plane",
                table: "feature_flag_tenants",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_feature_flags_key",
                schema: "control_plane",
                table: "feature_flags",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_feature_flags_last_changed_by",
                schema: "control_plane",
                table: "feature_flags",
                column: "last_changed_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_flag_changes",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "feature_flag_tenants",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "feature_flags",
                schema: "control_plane");
        }
    }
}
