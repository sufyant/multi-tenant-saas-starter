using Microsoft.Extensions.Options;
using Wolverine;

namespace Notifications.Application.Notifications.DeleteExpiredNotifications;

// U8: one batch in each transaction, so a large backlog never holds one long transaction or lock. A full batch sends the next one. The
// deletion runs across users through the narrow function, read or not; the transaction declares nobody.
public static class DeleteExpiredNotificationsHandler
{
    /// <summary>How many notifications one transaction deletes at most.</summary>
    public const int BatchSize = 1000;

    public static async Task<OutgoingMessages> HandleAsync(
        DeleteExpiredNotificationsCommand command,
        INotificationRepository notifications,
        IOptions<NotificationRetentionOptions> retention,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var deleted = await notifications.DeleteExpiredAsync(time.GetUtcNow().AddDays(-retention.Value.Days), BatchSize, cancellationToken);
        return deleted == BatchSize ? [new DeleteExpiredNotificationsCommand()] : [];
    }
}
