using Notifications.Domain;
using SharedKernel;

namespace Notifications.Application;

/// <summary>The errors the module answers, each with its code (N11w).</summary>
public static class Errors
{
    public static readonly Error NotificationNotFound = Error.NotFound(NotificationsErrorCodes.NotificationNotFound, "No such notification.");

    public static readonly Error NotificationPreferencesNotFound =
        Error.NotFound(NotificationsErrorCodes.NotificationPreferencesNotFound, "You are not a user yet.");

    public static readonly Error NotificationCategoryRequired =
        Error.Unprocessable(NotificationsErrorCodes.NotificationCategoryRequired, "This category is required: it cannot be turned off.");
}
