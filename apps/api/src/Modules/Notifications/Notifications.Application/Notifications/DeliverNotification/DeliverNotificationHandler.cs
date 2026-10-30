using Notifications.Application.EmailPreferences;
using Notifications.Application.Notifications.SendNotificationEmail;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using Wolverine;

namespace Notifications.Application.Notifications.DeliverNotification;

// The dispatcher (U4): for one recipient, it writes the in-app notification, which no preference turns off, and decides whether the email
// goes too: always for a required type, else as the recipient's preferences and the catalog's defaults say. The email is a message of its
// own, after the commit (O2): an email that fails for good goes to the dead letter queue (W11) and leaves the notification in place. The
// commit signals the recipient's bell (U6). A copy of the message finds the notification and does nothing (O4).
public static class DeliverNotificationHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        DeliverNotificationCommand deliver,
        INotificationRepository notifications,
        IEmailPreferenceRepository preferences,
        CancellationToken cancellationToken)
    {
        await notifications.DeclareUserAsync(deliver.UserId, cancellationToken);
        if (await notifications.ExistsAsync(deliver.NotificationId, cancellationToken))
        {
            return [];
        }

        notifications.Add(Notification.Create(deliver.NotificationId, deliver.UserId, deliver.TenantId, deliver.Type, deliver.Values, deliver.OccurredAt));
        await notifications.SaveAsync(cancellationToken);

        return deliver.Email is { } email && await preferences.SendsEmailAsync(deliver.UserId, NotificationCatalog.Of(deliver.Type), cancellationToken)
            ? [new SendNotificationEmailCommand(deliver.NotificationId, deliver.Type, deliver.Values, email, deliver.Language)]
            : [];
    }
}
