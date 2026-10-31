namespace Notifications.Domain.Notifications;

/// <summary>
/// One type of the catalog (U1): its category, the bell that shows it, where it leads, whether it is required, and the values its texts use.
/// Its category decides its channels and their defaults. A required type always reaches the person, so it is in a required category, and
/// a required category holds only required types (U4). A type whose link depends on its entitlement key says so in
/// <paramref name="LinkByKey"/>: the read then leads a key to its page, and any other key to <paramref name="Link"/>.
/// </summary>
public sealed record NotificationKind(
    string Type,
    string Category,
    NotificationBell Bell,
    NotificationLinkKind Link,
    bool Required,
    IReadOnlyList<NotificationValue> Values,
    bool LinkByKey = false)
{
    /// <summary>The name of the value whose entitlement key decides the link.</summary>
    public const string KeyValue = "keyName";
}
