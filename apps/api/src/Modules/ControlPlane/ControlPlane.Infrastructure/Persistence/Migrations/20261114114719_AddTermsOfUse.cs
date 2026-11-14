using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTermsOfUse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "terms_versions",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    requires_acceptance = table.Column<bool>(type: "boolean", nullable: false),
                    sends_email = table.Column<bool>(type: "boolean", nullable: false),
                    email_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terms_versions", x => x.id);
                    table.CheckConstraint("ck_terms_versions_cancellation", "(cancelled_at IS NULL) = (cancelled_by IS NULL)");
                    table.CheckConstraint("ck_terms_versions_email_sent", "sends_email = (email_sent_at IS NOT NULL)");
                    table.CheckConstraint("ck_terms_versions_url_https", "url LIKE 'https://%'");
                    table.ForeignKey(
                        name: "fk_terms_versions_users_cancelled_by",
                        column: x => x.cancelled_by,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_terms_versions_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "terms_acceptances",
                schema: "control_plane",
                columns: table => new
                {
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terms_acceptances", x => new { x.version_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_terms_acceptances_terms_versions_version_id",
                        column: x => x.version_id,
                        principalSchema: "control_plane",
                        principalTable: "terms_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_terms_acceptances_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "terms_notice_dismissals",
                schema: "control_plane",
                columns: table => new
                {
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dismissed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terms_notice_dismissals", x => new { x.version_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_terms_notice_dismissals_terms_versions_version_id",
                        column: x => x.version_id,
                        principalSchema: "control_plane",
                        principalTable: "terms_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_terms_notice_dismissals_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "control_plane",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_terms_acceptances_user_id",
                schema: "control_plane",
                table: "terms_acceptances",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_terms_acceptances_version_id_accepted_at_user_id",
                schema: "control_plane",
                table: "terms_acceptances",
                columns: new[] { "version_id", "accepted_at", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_terms_notice_dismissals_user_id",
                schema: "control_plane",
                table: "terms_notice_dismissals",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_terms_versions_cancelled_by",
                schema: "control_plane",
                table: "terms_versions",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "ix_terms_versions_created_by",
                schema: "control_plane",
                table: "terms_versions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_terms_versions_effective_date_created_at",
                schema: "control_plane",
                table: "terms_versions",
                columns: new[] { "effective_date", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terms_acceptances",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "terms_notice_dismissals",
                schema: "control_plane");

            migrationBuilder.DropTable(
                name: "terms_versions",
                schema: "control_plane");
        }
    }
}
