using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScreenReads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                schema: "control_plane",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "membership_count",
                schema: "control_plane",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "suspended_by",
                schema: "control_plane",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                schema: "control_plane",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateIndex(
                name: "ix_users_suspended_by",
                schema: "control_plane",
                table: "users",
                column: "suspended_by");

            migrationBuilder.AddForeignKey(
                name: "fk_users_users_suspended_by",
                schema: "control_plane",
                table: "users",
                column: "suspended_by",
                principalSchema: "control_plane",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // R13: the membership count of the users that exist, from their own memberships. Row level security holds the migration
            // account too, so the statement declares each user in turn (R11), as a user's deletion reads their memberships, and never a
            // tenant (R4). The tenants and users that exist keep the moment of this migration as their creation.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    account uuid;
                BEGIN
                    FOR account IN SELECT id FROM control_plane.users LOOP
                        PERFORM set_config('app.user_id', account::text, true);
                        UPDATE control_plane.users SET membership_count = (SELECT count(*) FROM control_plane.memberships) WHERE id = account;
                    END LOOP;
                    PERFORM set_config('app.user_id', '', true);
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_users_suspended_by",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_suspended_by",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "membership_count",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "suspended_by",
                schema: "control_plane",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "control_plane",
                table: "tenants");
        }
    }
}
