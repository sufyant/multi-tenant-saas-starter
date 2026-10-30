using ControlPlane.Contracts.Users;
using Notifications.Application.EmailPreferences;
using Notifications.Application.Notifications;

namespace Notifications.Application.Users.DeleteUserNotifications;

// A user's deletion deletes their notifications and their email preferences, in a transaction that declares the user (R4, U8;
// docs/flows/user-deletion.md). The event comes once the user has no membership left, without a tenant. A copy deletes nothing more.
public static class DeleteUserNotificationsHandler
{
    public static async Task HandleAsync(
        UserDeletedEvent deleted,
        INotificationRepository notifications,
        IEmailPreferenceRepository preferences,
        CancellationToken cancellationToken)
    {
        await notifications.DeclareUserAsync(deleted.UserId, cancellationToken);
        await notifications.DeleteAllAsync(cancellationToken);
        await preferences.DeleteAsync(cancellationToken);
    }
}
