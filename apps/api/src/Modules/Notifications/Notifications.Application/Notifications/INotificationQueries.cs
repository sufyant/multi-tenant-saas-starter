using SharedKernel;

namespace Notifications.Application.Notifications;

/// <summary>The reads of a bell, of the user that the transaction declares (U7).</summary>
public interface INotificationQueries
{
    internal Task DeclareUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>A page of the bell's notifications, newest first, one more than the limit when there are more (K5).</summary>
    internal Task<IReadOnlyList<NotificationView>> ListAsync(Inbox inbox, CursorRequest page, CancellationToken cancellationToken);

    internal Task<int> CountUnreadAsync(Inbox inbox, CancellationToken cancellationToken);
}
