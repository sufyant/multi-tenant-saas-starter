using Notifications.Application.Notifications.SendNotificationEmail;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using Wolverine;

namespace Notifications.Application.Notifications.DeliverNotification;

// The dispatcher (U4): for one recipient, it takes the type's channels from the catalog only; the person chooses none. The web channel
// writes the bell's notification; the commit signals the recipient's bell (U6). The email is a message of its own, after the
// commit (O2): an email that fails for good goes to the dead letter queue (W11) and leaves the notification in place. A type that goes by
// email only writes the record of its delivery in place of a notification. A copy of the message finds the notification or the record and
// does nothing (O4), at any time later: the email service keeps its key for a day only.
public static class DeliverNotificationHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        DeliverNotificationCommand deliver,
        INotificationRepository notifications,
        CancellationToken cancellationToken)
    {
        await notifications.DeclareUserAsync(deliver.UserId, cancellationToken);
        if (await notifications.ExistsAsync(deliver.NotificationId, cancellationToken))
        {
            return [];
        }

        var channels = NotificationCatalog.Of(deliver.Type).Channels;
        if (channels.Contains(NotificationChannel.Web))
        {
            notifications.Add(Notification.Create(deliver.NotificationId, deliver.UserId, deliver.TenantId, deliver.Type, deliver.Values, deliver.OccurredAt));
        }
        else
        {
            notifications.Add(new EmailDelivery(deliver.NotificationId, deliver.UserId, deliver.OccurredAt));
        }

        await notifications.SaveAsync(cancellationToken);

        return deliver.Email is { } email && channels.Contains(NotificationChannel.Email)
            ? [new SendNotificationEmailCommand(deliver.NotificationId, deliver.Type, deliver.Values, email, deliver.Language, deliver.TimeZone, deliver.People)]
            : [];
    }
}
