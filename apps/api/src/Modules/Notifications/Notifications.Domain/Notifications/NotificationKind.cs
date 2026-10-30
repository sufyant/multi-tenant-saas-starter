namespace Notifications.Domain.Notifications;

/// <summary>
/// One type of the catalog (U1): its category, the bell that shows it, where it leads, each channel it uses with its default, whether it is
/// required, and the values its texts use. A required type ignores the preferences and is not shown in them (U4). A type whose link depends
/// on its entitlement key says so in <paramref name="LinkByKey"/>: the read then leads a key to its page, and any other key to
/// <paramref name="Link"/>.
/// </summary>
public sealed record NotificationKind(
    string Type,
    string Category,
    NotificationBell Bell,
    NotificationLinkKind Link,
    IReadOnlyDictionary<NotificationChannel, bool> Channels,
    bool Required,
    IReadOnlyList<NotificationValue> Values,
    bool LinkByKey = false)
{
    /// <summary>The name of the value whose entitlement key decides the link.</summary>
    public const string KeyValue = "keyName";

    /// <summary>Whether the type sends an email when the person has not chosen.</summary>
    public bool EmailByDefault => Channels.TryGetValue(NotificationChannel.Email, out var on) && on;
}
