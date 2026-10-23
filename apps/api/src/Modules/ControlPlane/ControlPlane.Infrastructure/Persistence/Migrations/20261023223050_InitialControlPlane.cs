using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialControlPlane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "control_plane");

            // Privileges come from migrations: the application role works with the module's tables.
            migrationBuilder.Sql("""
                GRANT USAGE ON SCHEMA control_plane TO api_application;
                ALTER DEFAULT PRIVILEGES IN SCHEMA control_plane GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO api_application;
                """);

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "tenant_purge_sagas",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    waiting = table.Column<List<string>>(type: "text[]", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_purge_sagas", x => x.id);
                    table.CheckConstraint("ck_tenant_purge_sagas_state", "state IN ('PurgingModules', 'Finishing', 'Completed')");
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    suspension_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                    table.CheckConstraint("ck_tenants_purged_is_a_tombstone", "status <> 'Purged' OR (purged_at IS NOT NULL AND closed_at IS NULL AND suspension_reason IS NULL)");
                    table.CheckConstraint("ck_tenants_status", "status IN ('Active', 'Suspended', 'Closed', 'Purging', 'Purged')");
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    suspended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    language = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    membership_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    suspended_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_deleted_keeps_no_personal_data", "deleted_at IS NULL OR (email IS NULL AND first_name IS NULL AND last_name IS NULL AND time_zone IS NULL)");
                    table.CheckConstraint("ck_users_language", "language IN ('en', 'tr', 'ar')");
                    table.CheckConstraint("ck_users_membership_count", "membership_count >= 0");
                    table.CheckConstraint("ck_users_suspension", "(suspended_at IS NULL) = (suspended_by IS NULL)");
                    table.ForeignKey(
                        name: "fk_users_users_suspended_by",
                        column: x => x.suspended_by,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    built_in = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                    table.UniqueConstraint("ak_roles_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_roles_built_in", "built_in IN ('Owner', 'Admin', 'Member')");
                    table.ForeignKey(
                        name: "fk_roles_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "control_plane",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_summaries",
                schema: "control_plane",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    owner_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    owner_invitation_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    owner_invitation_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    owner_invitation_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    owner_invitation_language = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_summaries", x => x.tenant_id);
                    table.CheckConstraint("ck_tenant_summaries_counts", "member_count >= 0 AND owner_count >= 0 AND member_count >= owner_count");
                    table.CheckConstraint("ck_tenant_summaries_owner_invitation_language", "owner_invitation_language IN ('en', 'tr', 'ar')");
                    table.CheckConstraint("ck_tenant_summaries_owner_invitation_status", "owner_invitation_status IN ('Pending', 'Accepted', 'Cancelled')");
                    table.CheckConstraint("ck_tenant_summaries_owner_invitation_together", "(owner_invitation_status IS NULL) = (owner_invitation_expires_at IS NULL) AND (owner_invitation_status IS NOT NULL OR (owner_invitation_email IS NULL AND owner_invitation_language IS NULL))");
                    table.CheckConstraint("ck_tenant_summaries_owner_invitation_without_owner", "owner_count = 0 OR owner_invitation_status IS NULL");
                    table.CheckConstraint("ck_tenant_summaries_pending_owner_invitation_has_email", "owner_invitation_status <> 'Pending' OR owner_invitation_email IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_tenant_summaries_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "control_plane",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "system_admins",
                schema: "control_plane",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_admins", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_system_admins_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    role_id = table.Column<Guid>(type: "uuid", nullable: true),
                    secret_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    sign_up_invitation_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    language = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitations", x => x.id);
                    table.CheckConstraint("ck_invitations_acceptance", "(accepted_at IS NULL) = (accepted_by IS NULL) AND (status = 'Accepted') = (accepted_at IS NOT NULL)");
                    table.CheckConstraint("ck_invitations_language", "language IN ('en', 'tr', 'ar')");
                    table.CheckConstraint("ck_invitations_pending_has_email", "status <> 'Pending' OR email IS NOT NULL");
                    table.CheckConstraint("ck_invitations_pending_has_role", "status <> 'Pending' OR role_id IS NOT NULL");
                    table.CheckConstraint("ck_invitations_status", "status IN ('Pending', 'Accepted', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_invitations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "control_plane",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invitations_users_accepted_by",
                        column: x => x.accepted_by,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invitations_users_invited_by",
                        column: x => x.invited_by,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                schema: "control_plane",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memberships", x => new { x.tenant_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_memberships_roles_tenant_id_role_id",
                        columns: x => new { x.tenant_id, x.role_id },
                        principalSchema: "control_plane",
                        principalTable: "roles",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_memberships_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "control_plane",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_memberships_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "control_plane",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.tenant_id, x.role_id, x.permission });
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_tenant_id_role_id",
                        columns: x => new { x.tenant_id, x.role_id },
                        principalSchema: "control_plane",
                        principalTable: "roles",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invitations_accepted_by",
                schema: "control_plane",
                table: "invitations",
                column: "accepted_by");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_email",
                schema: "control_plane",
                table: "invitations",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_email_trgm",
                schema: "control_plane",
                table: "invitations",
                column: "email")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_invitations_expires_at",
                schema: "control_plane",
                table: "invitations",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_invited_by",
                schema: "control_plane",
                table: "invitations",
                column: "invited_by");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_pending_email",
                schema: "control_plane",
                table: "invitations",
                columns: new[] { "tenant_id", "email" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_secret_hash",
                schema: "control_plane",
                table: "invitations",
                column: "secret_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invitations_tenant_id_role_id",
                schema: "control_plane",
                table: "invitations",
                columns: new[] { "tenant_id", "role_id" });

            migrationBuilder.CreateIndex(
                name: "ix_memberships_tenant_id_role_id",
                schema: "control_plane",
                table: "memberships",
                columns: new[] { "tenant_id", "role_id" });

            migrationBuilder.CreateIndex(
                name: "ix_memberships_user_id",
                schema: "control_plane",
                table: "memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_name",
                schema: "control_plane",
                table: "roles",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_roles_name_trgm",
                schema: "control_plane",
                table: "roles",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id_built_in",
                schema: "control_plane",
                table: "roles",
                columns: new[] { "tenant_id", "built_in" },
                unique: true,
                filter: "built_in IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_purge_sagas_tenant_id",
                schema: "control_plane",
                table: "tenant_purge_sagas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_name",
                schema: "control_plane",
                table: "tenants",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_name_trgm",
                schema: "control_plane",
                table: "tenants",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                schema: "control_plane",
                table: "tenants",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug_trgm",
                schema: "control_plane",
                table: "tenants",
                column: "slug")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "control_plane",
                table: "users",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_users_email_trgm",
                schema: "control_plane",
                table: "users",
                column: "email")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_users_external_id",
                schema: "control_plane",
                table: "users",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_first_name_trgm",
                schema: "control_plane",
                table: "users",
                column: "first_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_users_last_name",
                schema: "control_plane",
                table: "users",
                column: "last_name");

            migrationBuilder.CreateIndex(
                name: "ix_users_last_name_trgm",
                schema: "control_plane",
                table: "users",
                column: "last_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_users_suspended_by",
                schema: "control_plane",
                table: "users",
                column: "suspended_by");

            // A3: a custom role that a membership or a pending invitation uses cannot be deleted; a finished invitation still names its
            // role, and loses it when the role is deleted. SET NULL (role_id) nulls the role only and keeps the tenant column, which is not
            // null (PostgreSQL 15 and later). EF Core cannot write a column list here, so the key is written in SQL (InvitationConfiguration).
            migrationBuilder.Sql("""
                ALTER TABLE control_plane.invitations ADD CONSTRAINT fk_invitations_roles_tenant_id_role_id
                    FOREIGN KEY (tenant_id, role_id) REFERENCES control_plane.roles (tenant_id, id) ON DELETE SET NULL (role_id);
                """);

            // A3: a role's name is unique in its tenant without regard to case, built-in names included. EF Core cannot model an index on
            // an expression, so it is written here (RoleConfiguration).
            migrationBuilder.Sql("CREATE UNIQUE INDEX ix_roles_tenant_id_lower_name ON control_plane.roles (tenant_id, lower(name));");

            // R11: GET /v1/me/tenants runs without a tenant, with the user declared instead, and reads that user's memberships in every
            // tenant. It is FOR SELECT only: permissive policies combine with OR, so a policy that allowed a write here would let a user add
            // themselves to any tenant.
            migrationBuilder.Sql(
                "CREATE POLICY own_memberships ON control_plane.memberships FOR SELECT "
                + "USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);");

            // R11: a user's own tenant list and the platform's user detail declare the user, not a tenant, and read the role of each of
            // the user's memberships. The policy shows those roles and no other. It is FOR SELECT only: permissive policies combine with OR,
            // so a policy that allowed a write here would let a user change a role.
            migrationBuilder.Sql("""
                CREATE POLICY own_roles ON control_plane.roles FOR SELECT USING (EXISTS (
                    SELECT FROM control_plane.memberships
                    WHERE memberships.tenant_id = roles.tenant_id AND memberships.role_id = roles.id
                        AND memberships.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid));
                """);

            // R11: a read that declares only the user sees the invitations the user accepted, in every tenant, so a user's deletion finds
            // the tenants the user joined, also where no membership is left (A8). It is FOR SELECT only: permissive policies combine with
            // OR, so a policy that allowed a write here would let a user change a tenant's invitation.
            migrationBuilder.Sql(
                "CREATE POLICY own_accepted_invitations ON control_plane.invitations FOR SELECT "
                + "USING (accepted_by = NULLIF(current_setting('app.user_id', true), '')::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitations",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "memberships",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "system_admins",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "tenant_purge_sagas",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "tenant_summaries",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "users",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "control_plane");

            migrationBuilder.Sql("""
                ALTER DEFAULT PRIVILEGES IN SCHEMA control_plane REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM api_application;
                REVOKE USAGE ON SCHEMA control_plane FROM api_application;
                """);
        }
    }
}
