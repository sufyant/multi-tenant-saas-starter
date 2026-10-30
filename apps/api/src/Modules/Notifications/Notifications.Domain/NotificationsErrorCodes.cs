using SharedKernel;

namespace Notifications.Domain;

/// <summary>The codes of every error Notifications answers, in its lowest layer, so that every layer of the module reaches them (N11w).</summary>
public static class NotificationsErrorCodes
{
    public static readonly ErrorCode NotificationNotFound = ErrorCode.Declare("notification.not_found");

    /// <summary>A signed-in person who is not a user yet has no email preferences to change.</summary>
    public static readonly ErrorCode EmailPreferencesNotFound = ErrorCode.Declare("email_preferences.not_found");
}
