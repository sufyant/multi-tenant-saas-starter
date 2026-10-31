using SharedKernel;

namespace Notifications.Application.NotificationPreferences.ChangeNotificationPreferences;

/// <summary>
/// The user's new notification preferences (U5): some main switches, by the name of their channel, and some categories in some channels.
/// Only a category that can reach the user is kept, so the endpoint passes them, and the reader's language, in which the answer names the
/// types.
/// </summary>
[MessageName("change-notification-preferences")]
public sealed record ChangeNotificationPreferencesCommand(
    Guid UserId,
    IReadOnlyDictionary<string, bool> Channels,
    IReadOnlyList<CategoryChoice> Categories,
    IReadOnlyList<string> Reachable,
    Language Language);
