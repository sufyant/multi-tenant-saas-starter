namespace Notifications.Application.EmailPreferences;

/// <summary>The reads of the email preferences of the user that the transaction declares, without tracking (Q2).</summary>
public interface IEmailPreferenceQueries
{
    internal Task DeclareUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The preferences as the screen shows them, of the categories given: the defaults when the user never chose.</summary>
    internal Task<EmailPreferencesView> FindAsync(Guid userId, IReadOnlyList<string> categories, CancellationToken cancellationToken);
}
