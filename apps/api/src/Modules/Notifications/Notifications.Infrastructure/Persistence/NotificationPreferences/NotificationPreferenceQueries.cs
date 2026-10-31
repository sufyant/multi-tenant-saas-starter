using Microsoft.EntityFrameworkCore;
using Notifications.Application.NotificationPreferences;
using Notifications.Domain.NotificationPreferences;
using SharedKernel;

namespace Notifications.Infrastructure.Persistence.NotificationPreferences;

/// <summary>The reads of the notification preferences on the module's DbContext, without tracking (Q2).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class NotificationPreferenceQueries(NotificationsDbContext notifications) : INotificationPreferenceQueries
{
    Task INotificationPreferenceQueries.DeclareUserAsync(Guid userId, CancellationToken cancellationToken) =>
        notifications.DeclareRecipientAsync(userId, cancellationToken);

    async Task<NotificationPreferencesView> INotificationPreferenceQueries.FindAsync(
        Guid userId, IReadOnlyList<string> categories, Language language, CancellationToken cancellationToken)
    {
        var preference = await notifications.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(found => found.UserId == userId, cancellationToken)
            ?? NotificationPreference.DefaultFor(userId);
        return NotificationPreferencesView.Of(preference, categories, language);
    }
}
