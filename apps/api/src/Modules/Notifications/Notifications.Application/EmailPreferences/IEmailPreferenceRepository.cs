using Notifications.Domain.EmailPreferences;

namespace Notifications.Application.EmailPreferences;

/// <summary>The email preferences of the user that the transaction declares (U5, R4).</summary>
public interface IEmailPreferenceRepository
{
    internal Task DeclareUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user's preferences, locked for a change; the defaults when the user never chose.</summary>
    internal Task<EmailPreference> FindEmailPreferenceForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Stores the preferences, or removes their row when every choice is the default.</summary>
    internal Task SaveAsync(EmailPreference preference, CancellationToken cancellationToken);

    /// <summary>Deletes the declared user's preferences, for the user's deletion.</summary>
    internal Task DeleteAsync(CancellationToken cancellationToken);
}
