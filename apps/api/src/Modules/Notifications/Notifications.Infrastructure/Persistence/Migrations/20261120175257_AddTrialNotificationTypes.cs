using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrialNotificationTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications",
                sql: "type IN ('feature_flags.ready_to_remove', 'feature_flags.unused', 'member.joined', 'member.removed', 'member.role_changed', 'tenant.closed', 'tenant.closed_for_owners', 'tenant.created', 'tenant.entitlement_override_removed', 'tenant.entitlement_override_set', 'tenant.meter_nearly_used', 'tenant.meter_used_up', 'tenant.moved_to_default_plan', 'tenant.owner_joined', 'tenant.payment_failed', 'tenant.payment_grace_started', 'tenant.plan_changed', 'tenant.price_change_announced', 'tenant.purged', 'tenant.purged_for_owners', 'tenant.reactivated', 'tenant.reactivated_for_owners', 'tenant.reopened', 'tenant.reopened_for_owners', 'tenant.spending_cap_nearly_reached', 'tenant.spending_cap_reached', 'tenant.suspended', 'tenant.suspended_for_owners', 'tenant.trial_ended', 'tenant.trial_ending', 'terms_version.created', 'user.data_export_ready')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                schema: "notifications",
                table: "notifications",
                sql: "type IN ('feature_flags.ready_to_remove', 'feature_flags.unused', 'member.joined', 'member.removed', 'member.role_changed', 'tenant.closed', 'tenant.closed_for_owners', 'tenant.entitlement_override_removed', 'tenant.entitlement_override_set', 'tenant.meter_nearly_used', 'tenant.meter_used_up', 'tenant.moved_to_default_plan', 'tenant.owner_joined', 'tenant.payment_failed', 'tenant.payment_grace_started', 'tenant.plan_changed', 'tenant.price_change_announced', 'tenant.purged', 'tenant.purged_for_owners', 'tenant.reactivated', 'tenant.reactivated_for_owners', 'tenant.reopened', 'tenant.reopened_for_owners', 'tenant.spending_cap_nearly_reached', 'tenant.spending_cap_reached', 'tenant.suspended', 'tenant.suspended_for_owners', 'terms_version.created', 'user.data_export_ready')");
        }
    }
}
