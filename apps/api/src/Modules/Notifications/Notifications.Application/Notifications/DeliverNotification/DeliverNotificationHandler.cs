using Notifications.Application.NotificationPreferences;
using Notifications.Application.Notifications.SendNotificationEmail;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using Wolverine;

namespace Notifications.Application.Notifications.DeliverNotification;

// The dispatcher (U4): for one recipient, it decides each channel from the recipient's preferences and the catalog. A channel goes when the
// type's category is required, or when the channel's main switch and the category's switch, or without a choice its default, are on. The
// web channel writes the bell's notification; the commit signals the recipient's bell (U6). The email is a message of its own, after the
// commit (O2): an email that fails for good goes to the dead letter queue (W11) and leaves the notification in place. A copy of the message
// finds the notification and does nothing (O4); with the web off there is no row to find, and the email's key, the notification's id,
// keeps a copy to one email.
public static class DeliverNotificationHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        DeliverNotificationCommand deliver,
        INotificationRepository notifications,
        INotificationPreferenceRepository preferences,
        CancellationToken cancellationToken)
    {
        await notifications.DeclareUserAsync(deliver.UserId, cancellationToken);
        if (await notifications.ExistsAsync(deliver.NotificationId, cancellationToken))
        {
            return [];
        }

        var channels = await preferences.ChannelsOfAsync(deliver.UserId, NotificationCatalog.Of(deliver.Type), cancellationToken);
        if (channels.Contains(NotificationChannel.Web))
        {
            notifications.Add(Notification.Create(deliver.NotificationId, deliver.UserId, deliver.TenantId, deliver.Type, deliver.Values, deliver.OccurredAt));
            await notifications.SaveAsync(cancellationToken);
        }

        return deliver.Email is { } email && channels.Contains(NotificationChannel.Email)
            ? [new SendNotificationEmailCommand(deliver.NotificationId, deliver.Type, deliver.Values, email, deliver.Language, deliver.TimeZone, deliver.People)]
            : [];
    }
}
