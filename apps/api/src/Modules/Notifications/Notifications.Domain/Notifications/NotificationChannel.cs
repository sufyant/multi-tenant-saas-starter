namespace Notifications.Domain.Notifications;

/// <summary>A way a notification reaches a person (U4), a column of the preferences. Push comes as a new member and a new adapter.</summary>
public enum NotificationChannel
{
    /// <summary>The bell of the web apps.</summary>
    Web,

    /// <summary>One generic notification email, through the email channel.</summary>
    Email,
}
