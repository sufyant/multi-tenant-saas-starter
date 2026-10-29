namespace Notifications.Application.EmailPreferences.GetEmailPreferences;

public static class GetEmailPreferencesHandler
{
    public static async Task<EmailPreferencesView> HandleAsync(GetEmailPreferencesQuery query, IEmailPreferenceRepository preferences, CancellationToken cancellationToken)
    {
        await preferences.DeclareUserAsync(query.UserId, cancellationToken);
        var preference = await preferences.FindEmailPreferenceForUpdateAsync(query.UserId, cancellationToken);
        return new(preference.Enabled, [.. query.Categories.Select(category => new CategoryEmailPreferenceView(category, preference.IsOn(category)))]);
    }
}
