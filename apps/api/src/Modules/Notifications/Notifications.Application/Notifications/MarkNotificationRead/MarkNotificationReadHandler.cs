using SharedKernel;

namespace Notifications.Application.Notifications.MarkNotificationRead;

// Row level security shows the reader only their own notifications, so another user's answers as one that does not exist (OWASP API1).
// Opening one twice changes nothing. A change of read state writes no audit entry: it is the reader's own view, not a change of the
// product's data (K3). The commit signals the reader's bell, so their other tabs read the count again (U6).
public static class MarkNotificationReadHandler
{
    public static async Task<Result> HandleAsync(
        MarkNotificationReadCommand command,
        INotificationRepository notifications,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (command.Reader.UserId is not { } userId)
        {
            return Errors.NotificationNotFound;
        }

        await notifications.DeclareUserAsync(userId, cancellationToken);
        if (await notifications.FindNotificationForUpdateAsync(command.NotificationId, cancellationToken) is not { } notification)
        {
            return Errors.NotificationNotFound;
        }

        notification.MarkRead(time.GetUtcNow());
        await notifications.SaveAsync(cancellationToken);
        return Result.Success();
    }
}
