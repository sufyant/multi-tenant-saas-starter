using SharedKernel;

namespace Notifications.Domain;

/// <summary>The codes of every error Notifications answers, in its lowest layer, so that every layer of the module reaches them (N11w).</summary>
public static class NotificationsErrorCodes
{
    public static readonly ErrorCode NotificationNotFound = ErrorCode.Declare("notification.not_found");

    /// <summary>A signed-in person who is not a user yet has no notification preferences to change.</summary>
    public static readonly ErrorCode NotificationPreferencesNotFound = ErrorCode.Declare("notification_preferences.not_found");

    /// <summary>A required category is on in every channel, so a choice of it is refused (U4).</summary>
    public static readonly ErrorCode NotificationCategoryRequired = ErrorCode.Declare("notification_preferences.category_required");
}
