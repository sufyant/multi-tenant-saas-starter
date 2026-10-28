using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlansAndEntitlements : Migration
    {
        // The seed plans' ids, fixed, so every environment has the same ones.
        private static readonly Guid Free = new("0199c000-0000-7000-8000-00000000f4ee");

        private static readonly Guid Pro = new("0199c000-0000-7000-8000-000000000940");

        private static readonly DateTimeOffset SeededAt = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_counts",
                schema: "control_plane",
                table: "tenant_summaries");


            migrationBuilder.AddColumn<int>(
                name: "custom_role_count",
                schema: "control_plane",
                table: "tenant_summaries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<List<DateTimeOffset>>(
                name: "pending_invitation_expiries",
                schema: "control_plane",
                table: "tenant_summaries",
                type: "timestamp with time zone[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateTable(
                name: "entitlement_overrides",
                schema: "control_plane",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: true),
                    maximum = table.Column<int>(type: "integer", nullable: true),
                    unlimited = table.Column<bool>(type: "boolean", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    set_by = table.Column<Guid>(type: "uuid", nullable: false),
                    set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entitlement_overrides", x => new { x.tenant_id, x.key });
                    table.CheckConstraint("ck_entitlement_overrides_key", "key IN ('members.max', 'roles.custom', 'roles.custom.max')");
                    table.CheckConstraint("ck_entitlement_overrides_maximum", "maximum >= 0");
                    table.CheckConstraint("ck_entitlement_overrides_value_of_its_kind", "(key IN ('roles.custom') AND enabled IS NOT NULL AND maximum IS NULL AND NOT unlimited) OR (key IN ('members.max', 'roles.custom.max') AND enabled IS NULL AND (maximum IS NOT NULL) <> unlimited)");
                    table.ForeignKey(
                        name: "fk_entitlement_overrides_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "control_plane",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_entitlement_overrides_users_set_by",
                        column: x => x.set_by,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "default_plans",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<bool>(type: "boolean", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_default_plans", x => x.id);
                    table.CheckConstraint("ck_default_plans_one_row", "id");
                    table.ForeignKey(
                        name: "fk_default_plans_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "control_plane",
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_entitlements",
                schema: "control_plane",
                columns: table => new
                {
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: true),
                    maximum = table.Column<int>(type: "integer", nullable: true),
                    unlimited = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_entitlements", x => new { x.plan_id, x.key });
                    table.CheckConstraint("ck_plan_entitlements_key", "key IN ('members.max', 'roles.custom', 'roles.custom.max')");
                    table.CheckConstraint("ck_plan_entitlements_maximum", "maximum >= 0");
                    table.CheckConstraint("ck_plan_entitlements_value_of_its_kind", "(key IN ('roles.custom') AND enabled IS NOT NULL AND maximum IS NULL AND NOT unlimited) OR (key IN ('members.max', 'roles.custom.max') AND enabled IS NULL AND (maximum IS NOT NULL) <> unlimited)");
                    table.ForeignKey(
                        name: "fk_plan_entitlements_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "control_plane",
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // The seed plans. Free is the default plan of a new tenant. Pro holds what every tenant has had until now: custom roles on, 20 of
            // them (the setting ControlPlane:Roles:MaxCustomRolesPerTenant, which this step removes), and members without a limit. So every
            // existing tenant moves to Pro, and the migration takes nothing away.
            migrationBuilder.Sql($"""
                INSERT INTO control_plane.plans (id, name, created_at) VALUES
                    ('{Free}', 'Free', '{SeededAt:O}'),
                    ('{Pro}', 'Pro', '{SeededAt:O}');
                INSERT INTO control_plane.plan_entitlements (plan_id, key, enabled, maximum, unlimited) VALUES
                    ('{Free}', 'roles.custom', false, NULL, false),
                    ('{Free}', 'roles.custom.max', NULL, 0, false),
                    ('{Free}', 'members.max', NULL, 5, false),
                    ('{Pro}', 'roles.custom', true, NULL, false),
                    ('{Pro}', 'roles.custom.max', NULL, 20, false),
                    ('{Pro}', 'members.max', NULL, NULL, true);
                INSERT INTO control_plane.default_plans (id, plan_id) VALUES (true, '{Free}');
                """);

            // A plan's name is unique without regard to case, as a role's is (K6b). EF Core cannot model an index on an expression.
            migrationBuilder.Sql("CREATE UNIQUE INDEX ix_plans_lower_name ON control_plane.plans (lower(name));");

            // Expand: the column's default is Free, the default plan, so a tenant that the version before this one creates during the deploy,
            // without the column, gets a plan too. This version always names the plan. A later release drops the default (ARCHITECTURE.md,
            // section 13). Every existing tenant then moves to Pro.
            migrationBuilder.AddColumn<Guid>(
                name: "plan_id",
                schema: "control_plane",
                table: "tenants",
                type: "uuid",
                nullable: false,
                defaultValue: Free);

            migrationBuilder.Sql($"UPDATE control_plane.tenants SET plan_id = '{Pro}';");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_plan_id",
                schema: "control_plane",
                table: "tenants",
                column: "plan_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_counts",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "member_count >= 0 AND owner_count >= 0 AND member_count >= owner_count AND custom_role_count >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_default_plans_plan_id",
                schema: "control_plane",
                table: "default_plans",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_entitlement_overrides_set_by",
                schema: "control_plane",
                table: "entitlement_overrides",
                column: "set_by");

            migrationBuilder.CreateIndex(
                name: "ix_plans_name",
                schema: "control_plane",
                table: "plans",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_plans_name_trgm",
                schema: "control_plane",
                table: "plans",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.AddForeignKey(
                name: "fk_tenants_plans_plan_id",
                schema: "control_plane",
                table: "tenants",
                column: "plan_id",
                principalSchema: "control_plane",
                principalTable: "plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // R12, R4's exception for derived data: the summary of each tenant that has data counts its custom roles and keeps the expiry of
            // each pending invitation. Both tables are under row level security, which binds the migration account too, so the block declares
            // each tenant in turn, in this transaction, and clears the declaration at the end.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    tenant record;
                BEGIN
                    FOR tenant IN SELECT id FROM control_plane.tenants WHERE status <> 'Purged' ORDER BY id LOOP
                        PERFORM set_config('app.tenant_id', tenant.id::text, true);
                        UPDATE control_plane.tenant_summaries SET
                            custom_role_count = (SELECT count(*) FROM control_plane.roles WHERE built_in IS NULL),
                            pending_invitation_expiries = COALESCE(
                                (SELECT array_agg(expires_at ORDER BY expires_at) FROM control_plane.invitations WHERE status = 'Pending'), '{}')
                        WHERE tenant_id = tenant.id;
                    END LOOP;
                    PERFORM set_config('app.tenant_id', '', true);
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tenants_plans_plan_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropTable(
                name: "default_plans",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "entitlement_overrides",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "plan_entitlements",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "plans",
                schema: "control_plane");

            migrationBuilder.DropIndex(
                name: "ix_tenants_plan_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_counts",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.DropColumn(
                name: "plan_id",
                schema: "control_plane",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "custom_role_count",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.DropColumn(
                name: "pending_invitation_expiries",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_counts",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "member_count >= 0 AND owner_count >= 0 AND member_count >= owner_count");
        }
    }
}
