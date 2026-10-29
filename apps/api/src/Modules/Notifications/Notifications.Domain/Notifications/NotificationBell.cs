namespace Notifications.Domain.Notifications;

/// <summary>Which bell shows a notification (U7). A notification belongs to a user, and carries the tenant it is about, or none.</summary>
public enum NotificationBell
{
    /// <summary>The tenant web's bell of the tenant the notification is about.</summary>
    Tenant,

    /// <summary>Every bell of the tenant web, and the pages of a signed-in user without a tenant: it is about no tenant the person can enter.</summary>
    Personal,

    /// <summary>The platform's bell.</summary>
    Platform,
}
