using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

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
                name: "roles",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    built_in = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_creation_sagas",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_email_timeout = table.Column<TimeSpan>(type: "interval", nullable: false),
                    cancellation_timeout = table.Column<TimeSpan>(type: "interval", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "NULLIF(current_setting('app.tenant_id', true), '')::uuid"),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_creation_sagas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    first_name = table.Column<string>(type: "text", nullable: true),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    suspended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    language = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false, defaultValue: "en"),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    membership_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    suspended_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_users_suspended_by",
                        column: x => x.suspended_by,
                        principalSchema: "control_plane",
                        principalTable: "users",
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
                        onDelete: ReferentialAction.Cascade);
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
                    cancellation_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    suspension_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    first_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    member_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    owner_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    owner_invitation_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    owner_invitation_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    owner_invitation_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenants_users_first_owner_id",
                        column: x => x.first_owner_id,
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
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    sign_up_invitation_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    language = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false, defaultValue: "en")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_invitations_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "control_plane",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_invitations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "control_plane",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memberships", x => new { x.tenant_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_memberships_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "control_plane",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_memberships_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "control_plane",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_memberships_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "control_plane",
                table: "roles",
                columns: new[] { "id", "built_in", "name" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-7000-8000-000000000001"), "Owner", "owner" },
                    { new Guid("00000000-0000-7000-8000-000000000002"), "Admin", "admin" },
                    { new Guid("00000000-0000-7000-8000-000000000003"), "Member", "member" }
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
                name: "ix_invitations_role_id",
                schema: "control_plane",
                table: "invitations",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_tenant_id",
                schema: "control_plane",
                table: "invitations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_token_hash",
                schema: "control_plane",
                table: "invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_memberships_role_id",
                schema: "control_plane",
                table: "memberships",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_user_id",
                schema: "control_plane",
                table: "memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_creation_sagas_tenant_id",
                schema: "control_plane",
                table: "tenant_creation_sagas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_first_owner_id",
                schema: "control_plane",
                table: "tenants",
                column: "first_owner_id");

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

            // K2, K6: one pending invitation for each tenant and address, compared without case. EF Core models no index on an
            // expression, so the model does not hold it; ControlPlaneDbContext names it.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ix_invitations_pending_email ON control_plane.invitations (tenant_id, lower(email)) WHERE status = 'Pending';");

            // R11: GET /v1/me/tenants runs without a tenant, with the user declared instead, and reads that user's memberships in every
            // tenant. The only hand-written policy. It is FOR SELECT only: permissive policies combine with OR, so a policy that allowed a
            // write here would let a user add themselves to any tenant.
            migrationBuilder.Sql(
                "CREATE POLICY own_memberships ON control_plane.memberships FOR SELECT "
                + "USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);");
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
                name: "system_admins",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "tenant_creation_sagas",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "users",
                schema: "control_plane");

            migrationBuilder.Sql("""
                ALTER DEFAULT PRIVILEGES IN SCHEMA control_plane REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM api_application;
                REVOKE USAGE ON SCHEMA control_plane FROM api_application;
                """);
        }
    }
}
