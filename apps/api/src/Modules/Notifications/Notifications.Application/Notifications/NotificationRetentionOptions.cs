namespace Notifications.Application.Notifications;

// How long a notification is kept, read or not (section Notifications:Retention, U8), validated at startup by the module's infrastructure.
public sealed class NotificationRetentionOptions
{
    public const string Section = "Notifications:Retention";

    public int Days { get; set; } = 90;

    // The deletion function refuses a moment less than a day old (U8).
    public bool IsValid => Days >= 1;
}
