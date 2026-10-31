namespace Notifications.Application.NotificationPreferences;

/// <summary>One category's row: whether it is required, its types, and its switch in each channel.</summary>
public sealed record CategoryPreferenceView(
    string Category, bool Required, IReadOnlyList<NotificationTypeNameView> Types, IReadOnlyList<ChannelPreferenceView> Channels);
