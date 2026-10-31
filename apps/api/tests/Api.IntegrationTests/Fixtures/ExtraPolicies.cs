namespace Api.IntegrationTests.Fixtures;

// R11: every row level security policy besides the tenant isolation policy of each tenant table, with the one command it allows.
// DatabaseTables fails on any other policy, and on a listed one the database does not have.
internal static class ExtraPolicies
{
    public static readonly IReadOnlyList<(string Table, string Name, string Command)> Policies =
    [
        ("audit.platform_entries", "platform_insert", "INSERT"),
        ("audit.platform_entries", "platform_read", "SELECT"),
        ("control_plane.invitations", "own_accepted_invitations", "SELECT"),
        ("control_plane.memberships", "own_memberships", "SELECT"),
        ("control_plane.roles", "own_roles", "SELECT"),
        ("notifications.email_preferences", "own_email_preferences", "ALL"),
        ("notifications.notification_preferences", "own_notification_preferences", "ALL"),
        ("notifications.notifications", "own_notifications", "ALL"),
        ("notifications.notifications", "owner_delete", "DELETE"),
        ("notifications.notifications", "owner_read", "SELECT"),
    ];
}
