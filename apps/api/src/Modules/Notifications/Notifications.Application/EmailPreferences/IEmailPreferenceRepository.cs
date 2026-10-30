using Notifications.Domain.EmailPreferences;
using Notifications.Domain.Notifications;

namespace Notifications.Application.EmailPreferences;

/// <summary>The email preferences of the user that the transaction declares (U5, R4).</summary>
public interface IEmailPreferenceRepository
{
    internal Task DeclareUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user's preferences, locked for a change; the defaults when the user never chose.</summary>
    internal Task<EmailPreference> FindEmailPreferenceForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a notification of this kind goes to the user by email too, read as a value, without a lock: the dispatcher changes no
    /// preference (K6a), and a change that commits while it reads applies from the next notification.
    /// </summary>
    internal Task<bool> SendsEmailAsync(Guid userId, NotificationKind kind, CancellationToken cancellationToken);

    /// <summary>Stores the preferences, or removes their row when every choice is the default.</summary>
    internal Task SaveAsync(EmailPreference preference, CancellationToken cancellationToken);

    /// <summary>Deletes the declared user's preferences, for the user's deletion.</summary>
    internal Task DeleteAsync(CancellationToken cancellationToken);
}
