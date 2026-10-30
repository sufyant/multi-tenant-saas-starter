namespace Notifications.Application.EmailPreferences.GetEmailPreferences;

public static class GetEmailPreferencesHandler
{
    public static async Task<EmailPreferencesView> HandleAsync(GetEmailPreferencesQuery query, IEmailPreferenceQueries preferences, CancellationToken cancellationToken)
    {
        await preferences.DeclareUserAsync(query.UserId, cancellationToken);
        return await preferences.FindAsync(query.UserId, query.Categories, cancellationToken);
    }
}
