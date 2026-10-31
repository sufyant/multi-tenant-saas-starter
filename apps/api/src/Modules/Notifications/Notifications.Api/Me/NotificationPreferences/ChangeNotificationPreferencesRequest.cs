namespace Notifications.Api.Me.NotificationPreferences;

/// <summary>
/// The new notification preferences: some main switches, and some categories in some channels. A switch left out keeps its value, a
/// category that cannot reach the user is not kept, and a required category is refused (<c>notification_preferences.category_required</c>).
/// </summary>
public sealed record ChangeNotificationPreferencesRequest(IReadOnlyList<ChannelPreferenceRequest> Channels, IReadOnlyList<CategoryPreferenceRequest> Categories);
