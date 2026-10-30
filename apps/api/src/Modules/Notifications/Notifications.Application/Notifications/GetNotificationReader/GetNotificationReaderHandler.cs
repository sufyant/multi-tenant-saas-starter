using ControlPlane.Contracts.Users;

namespace Notifications.Application.Notifications.GetNotificationReader;

// The reader comes from ControlPlane, in a query of its own, so each read of the bells runs in the transaction of one module's DbContext (W1).
public static class GetNotificationReaderHandler
{
    public static async Task<NotificationReader> HandleAsync(GetNotificationReaderQuery query, IUserDirectory users, CancellationToken cancellationToken) =>
        await users.FindContactAsync(query.ActorId, cancellationToken) is { } user
            ? new NotificationReader(user.UserId, user.Language, (await users.ListSystemAdminIdsAsync(cancellationToken)).Contains(user.UserId), user.TimeZone)
            : NotificationReader.Nobody;
}
