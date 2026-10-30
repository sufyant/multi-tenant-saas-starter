using ControlPlane.Contracts.Users;
using Notifications.Domain;
using SharedKernel;

namespace Notifications.Application.Notifications.GetNotificationNames;

// API composition (Richardson, "API composition"), as the audit logs name their people (K3): the notifications come from Notifications, and
// the names of the people they keep by user id from ControlPlane, in one call for the whole page. They are two queries: Wolverine gives a
// handler the transaction of one DbContext, and each of the two modules has its own (W1).
public static class GetNotificationNamesHandler
{
    public static async Task<CursorPage<NotificationView>> HandleAsync(GetNotificationNamesQuery query, IUserDirectory users, CancellationToken cancellationToken)
    {
        var people = query.Page.Items.SelectMany(notification => NotificationFanOut.PeopleOf(notification.Type, notification.Values)).ToHashSet();
        var names = people.Count == 0 ? new Dictionary<Guid, UserName>() : await users.FindNamesAsync(people, cancellationToken);
        return query.Page.Map(notification => ViewOf(notification, names, query.Reader));
    }

    private static NotificationView ViewOf(StoredNotification notification, IReadOnlyDictionary<Guid, UserName> names, NotificationReader reader)
    {
        var kind = NotificationCatalog.Of(notification.Type);
        var text = NotificationText.Render(notification.Type, notification.Values, names, reader.Language, reader.TimeZone);
        return new(
            notification.Id,
            notification.Type,
            kind.Category,
            text.Title,
            text.Body,
            NotificationLinks.Of(kind, notification.Values).ToString(),
            notification.TenantId,
            notification.CreatedAt,
            notification.ReadAt);
    }
}
