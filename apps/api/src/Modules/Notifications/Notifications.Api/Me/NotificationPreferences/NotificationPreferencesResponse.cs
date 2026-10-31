namespace Notifications.Api.Me.NotificationPreferences;

/// <summary>
/// The signed-in user's notification preferences (U5): a table whose columns are the channels, each with its main switch, and whose rows
/// are the categories that can reach the user, each with its types and its switch in each channel. The required category is listed,
/// locked.
/// </summary>
public sealed record NotificationPreferencesResponse(IReadOnlyList<ChannelPreferenceResponse> Channels, IReadOnlyList<CategoryPreferenceResponse> Categories);
