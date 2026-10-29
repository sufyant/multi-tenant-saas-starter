namespace Notifications.Domain.Notifications;

/// <summary>
/// One type of the catalog (U1): its category, the bell that shows it, where it leads, each channel it uses with its default, whether it is
/// required, and the values its texts use. A required type ignores the preferences and is not shown in them (U4).
/// </summary>
public sealed record NotificationKind(
    string Type,
    string Category,
    NotificationBell Bell,
    NotificationLinkKind Link,
    IReadOnlyDictionary<NotificationChannel, bool> Channels,
    bool Required,
    IReadOnlyList<NotificationValue> Values)
{
    /// <summary>Whether the type sends an email when the person has not chosen.</summary>
    public bool EmailByDefault => Channels.TryGetValue(NotificationChannel.Email, out var on) && on;
}
