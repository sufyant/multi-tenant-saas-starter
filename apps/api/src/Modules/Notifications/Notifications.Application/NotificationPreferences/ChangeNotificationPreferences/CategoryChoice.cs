namespace Notifications.Application.NotificationPreferences.ChangeNotificationPreferences;

/// <summary>A category's new switch in one channel, by the name of the channel.</summary>
public sealed record CategoryChoice(string Category, string Channel, bool Enabled);
