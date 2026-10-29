using Notifications.Domain.Notifications;

namespace Notifications.Application.Notifications;

/// <summary>
/// The notifications of the user that the transaction declares (U3, R4): row level security shows a transaction only that user's rows.
/// Each method works in the handler's transaction.
/// </summary>
public interface INotificationRepository
{
    /// <summary>Declares the user whose notifications the rest of the transaction reads and writes.</summary>
    internal Task DeclareUserAsync(Guid userId, CancellationToken cancellationToken);

    internal Task<bool> ExistsAsync(Guid notificationId, CancellationToken cancellationToken);

    internal void Add(Notification notification);

    /// <summary>The notification, locked for the change (K6a).</summary>
    internal Task<Notification?> FindNotificationForUpdateAsync(Guid notificationId, CancellationToken cancellationToken);

    /// <summary>Marks every unread notification of the bell read, and answers how many it marked.</summary>
    internal Task<int> MarkAllReadAsync(Inbox inbox, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Deletes every notification of the declared user, for the user's deletion.</summary>
    internal Task DeleteAllAsync(CancellationToken cancellationToken);

    /// <summary>Deletes every notification about the declared tenant, in every recipient's list, for the tenant's purge (U8).</summary>
    internal Task PurgeTenantAsync(CancellationToken cancellationToken);

    /// <summary>Deletes up to a batch of the notifications older than the moment, across users, and answers how many (U8).</summary>
    internal Task<int> DeleteExpiredAsync(DateTimeOffset before, int batch, CancellationToken cancellationToken);

    internal Task SaveAsync(CancellationToken cancellationToken);
}
