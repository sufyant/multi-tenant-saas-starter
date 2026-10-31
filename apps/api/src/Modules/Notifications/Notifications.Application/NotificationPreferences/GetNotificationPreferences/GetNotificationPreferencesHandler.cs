namespace Notifications.Application.NotificationPreferences.GetNotificationPreferences;

public static class GetNotificationPreferencesHandler
{
    public static async Task<NotificationPreferencesView> HandleAsync(
        GetNotificationPreferencesQuery query, INotificationPreferenceQueries preferences, CancellationToken cancellationToken)
    {
        await preferences.DeclareUserAsync(query.UserId, cancellationToken);
        return await preferences.FindAsync(query.UserId, query.Categories, query.Language, cancellationToken);
    }
}
