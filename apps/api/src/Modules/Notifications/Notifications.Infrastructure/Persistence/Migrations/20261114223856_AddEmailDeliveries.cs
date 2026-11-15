using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_deliveries",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_deliveries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_deliveries_user_id",
                schema: "notifications",
                table: "email_deliveries",
                column: "user_id");

            // R4, R11, U3: the table belongs to a user, as the notifications do. Row level security, forced, shows the application only the
            // rows of the user the transaction declares.
            migrationBuilder.Sql("""
                ALTER TABLE notifications.email_deliveries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE notifications.email_deliveries FORCE ROW LEVEL SECURITY;
                CREATE POLICY own_email_deliveries ON notifications.email_deliveries TO api_application
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_deliveries",
                schema: "notifications");
        }
    }
}
