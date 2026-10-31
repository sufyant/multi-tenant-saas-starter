namespace Notifications.Application.NotificationPreferences;

/// <summary>One channel's switch, by the name of its channel: the main switch of a column, or a category's switch in it.</summary>
public sealed record ChannelPreferenceView(string Channel, bool Enabled);
