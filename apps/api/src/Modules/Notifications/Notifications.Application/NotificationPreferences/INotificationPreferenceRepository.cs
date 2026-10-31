using Notifications.Domain.NotificationPreferences;
using Notifications.Domain.Notifications;

namespace Notifications.Application.NotificationPreferences;

/// <summary>The notification preferences of the user that the transaction declares (U5, R4).</summary>
public interface INotificationPreferenceRepository
{
    internal Task DeclareUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user's preferences, locked for a change; the defaults when the user never chose.</summary>
    internal Task<NotificationPreference> FindNotificationPreferenceForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The channels on which a notification of this kind goes to the user, read as a value, without a lock: the dispatcher changes no
    /// preference (K6a), and a change that commits while it reads applies from the next notification.
    /// </summary>
    internal Task<IReadOnlySet<NotificationChannel>> ChannelsOfAsync(Guid userId, NotificationKind kind, CancellationToken cancellationToken);

    /// <summary>Stores the preferences, or removes their row when every choice is the default.</summary>
    internal Task SaveAsync(NotificationPreference preference, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the declared user's preferences, for the user's deletion, and their row of the table of the version before,
    /// <c>email_preferences</c>, until a later release drops it (section 13 of ARCHITECTURE.md).
    /// </summary>
    internal Task DeleteAsync(CancellationToken cancellationToken);
}
