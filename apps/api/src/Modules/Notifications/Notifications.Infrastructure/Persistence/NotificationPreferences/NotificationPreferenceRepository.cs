using Microsoft.EntityFrameworkCore;
using Notifications.Application.NotificationPreferences;
using Notifications.Domain.NotificationPreferences;
using Notifications.Domain.Notifications;

namespace Notifications.Infrastructure.Persistence.NotificationPreferences;

/// <summary>The notification preferences on the module's DbContext, in the transaction Wolverine began for the handler.</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class NotificationPreferenceRepository(NotificationsDbContext notifications) : INotificationPreferenceRepository
{
    Task INotificationPreferenceRepository.DeclareUserAsync(Guid userId, CancellationToken cancellationToken) =>
        notifications.DeclareRecipientAsync(userId, cancellationToken);

    async Task<NotificationPreference> INotificationPreferenceRepository.FindNotificationPreferenceForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        await notifications.NotificationPreferences
            .FromSql($"SELECT * FROM notifications.notification_preferences WHERE user_id = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
        ?? NotificationPreference.DefaultFor(userId);

    async Task<IReadOnlySet<NotificationChannel>> INotificationPreferenceRepository.ChannelsOfAsync(Guid userId, NotificationKind kind, CancellationToken cancellationToken)
    {
        var preference = await notifications.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(found => found.UserId == userId, cancellationToken)
            ?? NotificationPreference.DefaultFor(userId);
        return Enum.GetValues<NotificationChannel>().Where(channel => preference.Sends(kind, channel)).ToHashSet();
    }

    async Task INotificationPreferenceRepository.SaveAsync(NotificationPreference preference, CancellationToken cancellationToken)
    {
        var entry = notifications.Entry(preference);
        if (preference.IsDefault)
        {
            if (entry.State != EntityState.Detached)
            {
                notifications.NotificationPreferences.Remove(preference);
            }
        }
        else if (entry.State == EntityState.Detached)
        {
            notifications.NotificationPreferences.Add(preference);
        }

        await notifications.SaveChangesAsync(cancellationToken);
    }

    // The table of the version before keeps the declared user's row until a later release drops it, so a deletion empties it too. Row
    // level security shows only the declared user's row (R6).
    async Task INotificationPreferenceRepository.DeleteAsync(CancellationToken cancellationToken)
    {
        await notifications.NotificationPreferences.ExecuteDeleteAsync(cancellationToken);
        await notifications.Database.ExecuteSqlAsync($"DELETE FROM notifications.email_preferences", cancellationToken);
    }
}
