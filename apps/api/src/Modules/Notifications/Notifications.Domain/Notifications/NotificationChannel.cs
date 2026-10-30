namespace Notifications.Domain.Notifications;

/// <summary>A way a notification reaches a person (U4). Push comes as a new member and a new adapter.</summary>
public enum NotificationChannel
{
    /// <summary>The bell. It cannot be turned off: it is an inbox and disturbs nobody outside the product.</summary>
    InApp,

    /// <summary>One generic notification email, through the email channel.</summary>
    Email,
}
