using SharedKernel;

namespace Notifications.Application.NotificationPreferences.GetNotificationPreferences;

/// <summary>The user's notification preferences, of the categories that can reach them, with the types' names in their language.</summary>
[MessageName("get-notification-preferences")]
public sealed record GetNotificationPreferencesQuery(Guid UserId, IReadOnlyList<string> Categories, Language Language);
