using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GiveEachTenantItsRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A2: each tenant gets its own Owner, Admin and Member roles, and each membership and invitation moves from the role that every
            // tenant shared to its own tenant's role of the same kind. The roles are not under row level security until the tenant column
            // becomes theirs below, so they are written first; the memberships and the invitations are, so they move with each tenant
            // declared in turn (R4).
            migrationBuilder.DropForeignKey(
                name: "fk_invitations_roles_role_id",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.DropForeignKey(
                name: "fk_memberships_roles_role_id",
                schema: "control_plane",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "ix_memberships_role_id",
                schema: "control_plane",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "ix_invitations_role_id",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.AlterColumn<string>(
                name: "built_in",
                schema: "control_plane",
                table: "roles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.Sql("""
                ALTER TABLE control_plane.roles ADD COLUMN tenant_id uuid;
                INSERT INTO control_plane.roles (id, tenant_id, name, built_in)
                SELECT gen_random_uuid(), tenants.id, shared.name, shared.built_in
                FROM control_plane.tenants CROSS JOIN control_plane.roles AS shared WHERE shared.tenant_id IS NULL;
                DO $$
                DECLARE
                    tenant uuid;
                BEGIN
                    FOR tenant IN SELECT id FROM control_plane.tenants LOOP
                        PERFORM set_config('app.tenant_id', tenant::text, true);
                        UPDATE control_plane.memberships SET role_id = own.id
                        FROM control_plane.roles AS shared, control_plane.roles AS own
                        WHERE shared.id = memberships.role_id AND shared.tenant_id IS NULL AND own.tenant_id = tenant AND own.built_in = shared.built_in;
                        UPDATE control_plane.invitations SET role_id = own.id
                        FROM control_plane.roles AS shared, control_plane.roles AS own
                        WHERE shared.id = invitations.role_id AND shared.tenant_id IS NULL AND own.tenant_id = tenant AND own.built_in = shared.built_in;
                    END LOOP;
                    PERFORM set_config('app.tenant_id', '', true);
                END $$;
                DELETE FROM control_plane.roles WHERE tenant_id IS NULL;
                """);

            // The column becomes the tenant column, so the table comes under the standard policy (R1, TenantMigrationsSqlGenerator).
            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "control_plane",
                table: "roles",
                type: "uuid",
                nullable: false,
                defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_roles_tenant_id_id",
                schema: "control_plane",
                table: "roles",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id",
                schema: "control_plane",
                table: "roles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id_built_in",
                schema: "control_plane",
                table: "roles",
                columns: new[] { "tenant_id", "built_in" },
                unique: true,
                filter: "built_in IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_roles_built_in",
                schema: "control_plane",
                table: "roles",
                sql: "built_in IN ('Owner', 'Admin', 'Member')");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_tenant_id_role_id",
                schema: "control_plane",
                table: "memberships",
                columns: new[] { "tenant_id", "role_id" });

            migrationBuilder.CreateIndex(
                name: "ix_invitations_tenant_id_role_id",
                schema: "control_plane",
                table: "invitations",
                columns: new[] { "tenant_id", "role_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_invitations_roles_tenant_id_role_id",
                schema: "control_plane",
                table: "invitations",
                columns: new[] { "tenant_id", "role_id" },
                principalSchema: "control_plane",
                principalTable: "roles",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_memberships_roles_tenant_id_role_id",
                schema: "control_plane",
                table: "memberships",
                columns: new[] { "tenant_id", "role_id" },
                principalSchema: "control_plane",
                principalTable: "roles",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_roles_tenants_tenant_id",
                schema: "control_plane",
                table: "roles",
                column: "tenant_id",
                principalSchema: "control_plane",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // R11: a user's own tenant list and the platform's user detail declare the user, not a tenant, and read the role of each of
            // the user's memberships. The policy shows those roles and no other. It is FOR SELECT only: permissive policies combine with OR,
            // so a policy that allowed a write here would let a user change a role.
            migrationBuilder.Sql("""
                CREATE POLICY own_roles ON control_plane.roles FOR SELECT USING (EXISTS (
                    SELECT FROM control_plane.memberships
                    WHERE memberships.tenant_id = roles.tenant_id AND memberships.role_id = roles.id
                        AND memberships.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY own_roles ON control_plane.roles;
                DROP POLICY tenant_isolation ON control_plane.roles;
                ALTER TABLE control_plane.roles NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE control_plane.roles DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_invitations_roles_tenant_id_role_id",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.DropForeignKey(
                name: "fk_memberships_roles_tenant_id_role_id",
                schema: "control_plane",
                table: "memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_roles_tenants_tenant_id",
                schema: "control_plane",
                table: "roles");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_roles_tenant_id_id",
                schema: "control_plane",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_roles_tenant_id",
                schema: "control_plane",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_roles_tenant_id_built_in",
                schema: "control_plane",
                table: "roles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_roles_built_in",
                schema: "control_plane",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_memberships_tenant_id_role_id",
                schema: "control_plane",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "ix_invitations_tenant_id_role_id",
                schema: "control_plane",
                table: "invitations");

            migrationBuilder.Sql("""
                ALTER TABLE control_plane.roles ALTER COLUMN tenant_id DROP NOT NULL, ALTER COLUMN tenant_id DROP DEFAULT;
                INSERT INTO control_plane.roles (id, name, built_in) VALUES
                    ('00000000-0000-7000-8000-000000000001', 'owner', 'Owner'),
                    ('00000000-0000-7000-8000-000000000002', 'admin', 'Admin'),
                    ('00000000-0000-7000-8000-000000000003', 'member', 'Member');
                DO $$
                DECLARE
                    tenant uuid;
                BEGIN
                    FOR tenant IN SELECT id FROM control_plane.tenants LOOP
                        PERFORM set_config('app.tenant_id', tenant::text, true);
                        UPDATE control_plane.memberships SET role_id = shared.id
                        FROM control_plane.roles AS own, control_plane.roles AS shared
                        WHERE own.id = memberships.role_id AND own.tenant_id = tenant AND shared.tenant_id IS NULL AND shared.built_in = own.built_in;
                        UPDATE control_plane.invitations SET role_id = shared.id
                        FROM control_plane.roles AS own, control_plane.roles AS shared
                        WHERE own.id = invitations.role_id AND own.tenant_id = tenant AND shared.tenant_id IS NULL AND shared.built_in = own.built_in;
                    END LOOP;
                    PERFORM set_config('app.tenant_id', '', true);
                END $$;
                DELETE FROM control_plane.roles WHERE tenant_id IS NOT NULL;
                ALTER TABLE control_plane.roles DROP COLUMN tenant_id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "built_in",
                schema: "control_plane",
                table: "roles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_memberships_role_id",
                schema: "control_plane",
                table: "memberships",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_role_id",
                schema: "control_plane",
                table: "invitations",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "fk_invitations_roles_role_id",
                schema: "control_plane",
                table: "invitations",
                column: "role_id",
                principalSchema: "control_plane",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_memberships_roles_role_id",
                schema: "control_plane",
                table: "memberships",
                column: "role_id",
                principalSchema: "control_plane",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
