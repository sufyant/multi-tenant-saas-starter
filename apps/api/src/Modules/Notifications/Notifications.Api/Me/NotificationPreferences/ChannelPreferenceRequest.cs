namespace Notifications.Api.Me.NotificationPreferences;

/// <summary>A channel's new main switch, "All".</summary>
/// <param name="Channel">The channel, a column of the screen.</param>
/// <param name="Enabled">Whether any category that is not required goes on the channel.</param>
public sealed record ChannelPreferenceRequest(NotificationChannelKind Channel, bool Enabled);
