namespace Notifications.Api.Me.NotificationPreferences;

/// <summary>A channel's switch: the main switch of a column, or a category's switch in it.</summary>
public sealed record ChannelPreferenceResponse(NotificationChannelKind Channel, bool Enabled);
