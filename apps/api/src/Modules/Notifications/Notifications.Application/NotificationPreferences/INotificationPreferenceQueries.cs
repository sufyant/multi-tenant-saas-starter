using SharedKernel;

namespace Notifications.Application.NotificationPreferences;

/// <summary>The reads of the notification preferences of the user that the transaction declares, without tracking (Q2).</summary>
public interface INotificationPreferenceQueries
{
    internal Task DeclareUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The preferences as the screen shows them, of the categories given, with the types' names in the language: the defaults when the user
    /// never chose.
    /// </summary>
    internal Task<NotificationPreferencesView> FindAsync(Guid userId, IReadOnlyList<string> categories, Language language, CancellationToken cancellationToken);
}
