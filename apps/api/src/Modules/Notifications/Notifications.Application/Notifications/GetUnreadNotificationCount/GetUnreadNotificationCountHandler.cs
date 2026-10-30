namespace Notifications.Application.Notifications.GetUnreadNotificationCount;

public static class GetUnreadNotificationCountHandler
{
    public static async Task<int> HandleAsync(GetUnreadNotificationCountQuery query, INotificationQueries notifications, CancellationToken cancellationToken)
    {
        if (query.Reader.UserId is not { } userId)
        {
            return 0;
        }

        await notifications.DeclareUserAsync(userId, cancellationToken);
        return await notifications.CountUnreadAsync(query.Inbox, cancellationToken);
    }
}
