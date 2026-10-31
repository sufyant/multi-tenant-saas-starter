namespace Notifications.Api.Me.NotificationPreferences;

/// <summary>A category's row.</summary>
/// <param name="Category">The category, from the enum NotificationCategory.</param>
/// <param name="Required">Whether the category is required: on in every channel and locked, whatever the main switches say.</param>
/// <param name="Types">The types of the category; they cannot be chosen one by one.</param>
/// <param name="Channels">The category's switch in each channel, in the order of the columns.</param>
public sealed record CategoryPreferenceResponse(
    string Category, bool Required, IReadOnlyList<NotificationTypeNameResponse> Types, IReadOnlyList<ChannelPreferenceResponse> Channels);
