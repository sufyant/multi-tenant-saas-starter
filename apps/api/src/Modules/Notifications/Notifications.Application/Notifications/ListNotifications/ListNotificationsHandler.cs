using SharedKernel;

namespace Notifications.Application.Notifications.ListNotifications;

// The read declares the reader, never the tenant: the tenant of a tenant's bell is a filter of the reader's own rows, which the tenant door
// has proved (U7). A person who is not a user yet has no notification. K5's exception, as the audit logs: a cursor, newest first, no total.
public static class ListNotificationsHandler
{
    public static async Task<Result<CursorPage<StoredNotification>>> HandleAsync(ListNotificationsQuery query, INotificationQueries notifications, CancellationToken cancellationToken)
    {
        if (query.Reader.UserId is not { } userId)
        {
            return new CursorPage<StoredNotification>([], null);
        }

        await notifications.DeclareUserAsync(userId, cancellationToken);
        var found = await notifications.ListAsync(query.Inbox, query.Page, cancellationToken);
        return CursorPage.Of(found, query.Page, notification => new CursorPosition(notification.CreatedAt, notification.Id));
    }
}
