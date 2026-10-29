using Microsoft.EntityFrameworkCore;
using Notifications.Application.Notifications;
using Notifications.Domain.Notifications;

namespace Notifications.Infrastructure.Persistence.Notifications;

/// <summary>The notifications on the module's DbContext, in the transaction Wolverine began for the handler.</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class NotificationRepository(NotificationsDbContext notifications) : INotificationRepository
{
    Task INotificationRepository.DeclareUserAsync(Guid userId, CancellationToken cancellationToken) =>
        notifications.DeclareRecipientAsync(userId, cancellationToken);

    Task<bool> INotificationRepository.ExistsAsync(Guid notificationId, CancellationToken cancellationToken) =>
        notifications.Notifications.AnyAsync(notification => notification.Id == notificationId, cancellationToken);

    void INotificationRepository.Add(Notification notification) => notifications.Notifications.Add(notification);

    Task<Notification?> INotificationRepository.FindNotificationForUpdateAsync(Guid notificationId, CancellationToken cancellationToken) =>
        notifications.Notifications
            .FromSql($"SELECT * FROM notifications.notifications WHERE id = {notificationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    async Task<int> INotificationRepository.MarkAllReadAsync(Inbox inbox, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var marked = await notifications.Notifications
            .Where(NotificationQueries.Shows(inbox))
            .Where(notification => notification.ReadAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(notification => notification.ReadAt, now), cancellationToken);

        // The change tracker does not see a bulk update, so the user's live signal is named here (U6).
        if (marked > 0)
        {
            notifications.SignalAfterCommit(inbox);
        }

        return marked;
    }

    Task INotificationRepository.DeleteAllAsync(CancellationToken cancellationToken) =>
        notifications.Notifications.ExecuteDeleteAsync(cancellationToken);

    // U8: the application may only execute the function, which deletes the notifications about the tenant the transaction declares.
    async Task INotificationRepository.PurgeTenantAsync(CancellationToken cancellationToken)
    {
        _ = notifications.MessageTenantId ?? throw new InvalidOperationException("A tenant's notifications are purged inside the tenant.");
        await notifications.Database.ExecuteSqlAsync($"SELECT notifications.purge_tenant_notifications()", cancellationToken);
    }

    async Task<int> INotificationRepository.DeleteExpiredAsync(DateTimeOffset before, int batch, CancellationToken cancellationToken) =>
        await notifications.Database
            .SqlQuery<int>($"SELECT notifications.delete_expired_notifications({before}, {batch}) AS \"Value\"")
            .SingleAsync(cancellationToken);

    Task INotificationRepository.SaveAsync(CancellationToken cancellationToken) => notifications.SaveChangesAsync(cancellationToken);
}
