using Notifications.Domain;
using SharedKernel;

namespace Notifications.Application;

/// <summary>The errors the module answers, each with its code (N11w).</summary>
public static class Errors
{
    public static readonly Error NotificationNotFound = Error.NotFound(NotificationsErrorCodes.NotificationNotFound, "No such notification.");

    public static readonly Error EmailPreferencesNotFound = Error.NotFound(NotificationsErrorCodes.EmailPreferencesNotFound, "You are not a user yet.");
}
