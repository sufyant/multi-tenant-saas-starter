using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_counts",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_roles_built_in",
                schema: "control_plane",
                table: "roles");

            migrationBuilder.AddColumn<List<DateTimeOffset>>(
                name: "free_invitation_expiries",
                schema: "control_plane",
                table: "tenant_summaries",
                type: "timestamp with time zone[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<int>(
                name: "free_member_count",
                schema: "control_plane",
                table: "tenant_summaries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_counts",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "member_count >= 0 AND owner_count >= 0 AND member_count >= owner_count AND custom_role_count >= 0 AND free_member_count >= 0 AND member_count >= free_member_count");

            migrationBuilder.AddCheckConstraint(
                name: "ck_roles_built_in",
                schema: "control_plane",
                table: "roles",
                sql: "built_in IN ('Owner', 'Admin', 'Member', 'BillingManager')");

            // A2: every tenant that has data gets its Billing manager role, as a new tenant's creation writes it. The roles are under row
            // level security, which binds the migration account too, so the block declares each tenant in turn, in this transaction, and
            // clears the declaration at the end. A custom role named as the new role would break the unique name (A3), so it keeps its
            // name with " (custom)" after it. No member or invitation has the role yet, so every summary's seats stay right.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    tenant record;
                BEGIN
                    FOR tenant IN SELECT id FROM control_plane.tenants WHERE status <> 'Purged' ORDER BY id LOOP
                        PERFORM set_config('app.tenant_id', tenant.id::text, true);
                        UPDATE control_plane.roles SET name = left(name, 91) || ' (custom)'
                        WHERE built_in IS NULL AND lower(name) = 'billing_manager';
                        INSERT INTO control_plane.roles (id, tenant_id, name, built_in)
                        VALUES (uuidv7(), tenant.id, 'billing_manager', 'BillingManager');
                    END LOOP;
                    PERFORM set_config('app.tenant_id', '', true);
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The members and the pending invitations of the Billing manager role become Members, the role with the least access, before
            // the role goes, because a membership and an invitation restrict the deletion of their role (R16).
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    tenant record;
                BEGIN
                    FOR tenant IN SELECT id FROM control_plane.tenants WHERE status <> 'Purged' ORDER BY id LOOP
                        PERFORM set_config('app.tenant_id', tenant.id::text, true);
                        UPDATE control_plane.memberships SET role_id = (SELECT id FROM control_plane.roles WHERE built_in = 'Member')
                        WHERE role_id = (SELECT id FROM control_plane.roles WHERE built_in = 'BillingManager');
                        UPDATE control_plane.invitations SET role_id = (SELECT id FROM control_plane.roles WHERE built_in = 'Member')
                        WHERE role_id = (SELECT id FROM control_plane.roles WHERE built_in = 'BillingManager');
                        DELETE FROM control_plane.roles WHERE built_in = 'BillingManager';
                    END LOOP;
                    PERFORM set_config('app.tenant_id', '', true);
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_summaries_counts",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_roles_built_in",
                schema: "control_plane",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "free_invitation_expiries",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.DropColumn(
                name: "free_member_count",
                schema: "control_plane",
                table: "tenant_summaries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_summaries_counts",
                schema: "control_plane",
                table: "tenant_summaries",
                sql: "member_count >= 0 AND owner_count >= 0 AND member_count >= owner_count AND custom_role_count >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_roles_built_in",
                schema: "control_plane",
                table: "roles",
                sql: "built_in IN ('Owner', 'Admin', 'Member')");
        }
    }
}
