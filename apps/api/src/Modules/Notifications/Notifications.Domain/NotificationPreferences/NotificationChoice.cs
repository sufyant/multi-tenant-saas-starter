using Notifications.Domain.Notifications;

namespace Notifications.Domain.NotificationPreferences;

/// <summary>A person's choice of one category on one channel (U5).</summary>
public sealed record NotificationChoice(string Category, NotificationChannel Channel, bool Enabled);
