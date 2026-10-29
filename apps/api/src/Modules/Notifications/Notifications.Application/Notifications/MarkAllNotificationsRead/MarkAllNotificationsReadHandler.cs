using SharedKernel;

namespace Notifications.Application.Notifications.MarkAllNotificationsRead;

// One statement marks the bell's unread notifications read; a bell that has none changes nothing. It writes no audit entry (K3).
public static class MarkAllNotificationsReadHandler
{
    public static async Task<Result> HandleAsync(
        MarkAllNotificationsReadCommand command,
        INotificationRepository notifications,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (command.Reader.UserId is { } userId)
        {
            await notifications.DeclareUserAsync(userId, cancellationToken);
            await notifications.MarkAllReadAsync(command.Inbox, time.GetUtcNow(), cancellationToken);
        }

        return Result.Success();
    }
}
