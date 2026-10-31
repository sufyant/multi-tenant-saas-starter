namespace Notifications.Api.Me.NotificationPreferences;

/// <summary>A category's new switch in one channel.</summary>
/// <param name="Category">The category, from the enum NotificationCategory. A required category is refused.</param>
/// <param name="Channel">The channel of the switch.</param>
/// <param name="Enabled">Whether a notification of the category goes on the channel.</param>
public sealed record CategoryPreferenceRequest(string Category, NotificationChannelKind Channel, bool Enabled);
