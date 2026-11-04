using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredFilesAndVerificationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stored_files",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    object_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stored_files", x => x.id);
                    table.CheckConstraint("ck_stored_files_purpose", "purpose IN ('DataExport')");
                    table.CheckConstraint("ck_stored_files_ready_has_size_and_expiry", "status = 'Preparing' OR status = 'Failed' OR (size_bytes IS NOT NULL AND ready_at IS NOT NULL AND expires_at IS NOT NULL)");
                    table.CheckConstraint("ck_stored_files_status", "status IN ('Preparing', 'Ready', 'Failed', 'Expired')");
                    table.ForeignKey(
                        name: "fk_stored_files_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "verification_codes",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    code_hash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    new_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_verification_codes", x => x.id);
                    table.CheckConstraint("ck_verification_codes_attempts", "attempts >= 0");
                    table.CheckConstraint("ck_verification_codes_new_email", "(purpose = 'EmailChange') = (new_email IS NOT NULL)");
                    table.CheckConstraint("ck_verification_codes_purpose", "purpose IN ('EmailChange', 'AccountDeletion')");
                    table.ForeignKey(
                        name: "fk_verification_codes_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_object_key",
                schema: "control_plane",
                table: "stored_files",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_user_id_created_at",
                schema: "control_plane",
                table: "stored_files",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_verification_codes_user_id_purpose_created_at",
                schema: "control_plane",
                table: "verification_codes",
                columns: new[] { "user_id", "purpose", "created_at" });

            // R4, R11: both tables belong to a user, not to a tenant (R6), as Notifications' table does. Row level security, forced, shows
            // the application only the rows of the user the transaction declares, for every command: a transaction that declares a tenant,
            // or nobody, sees none.
            migrationBuilder.Sql("""
                ALTER TABLE control_plane.stored_files ENABLE ROW LEVEL SECURITY;
                ALTER TABLE control_plane.stored_files FORCE ROW LEVEL SECURITY;
                CREATE POLICY own_files ON control_plane.stored_files TO api_application
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                ALTER TABLE control_plane.verification_codes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE control_plane.verification_codes FORCE ROW LEVEL SECURITY;
                CREATE POLICY own_verification_codes ON control_plane.verification_codes TO api_application
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stored_files",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "verification_codes",
                schema: "control_plane");
        }
    }
}
