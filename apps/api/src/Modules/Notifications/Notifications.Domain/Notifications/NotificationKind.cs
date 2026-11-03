namespace Notifications.Domain.Notifications;

/// <summary>
/// One type of the catalog (U1): the bell that shows it, where it leads, the channels it goes on, and the values its texts use. The product
/// fixes the channels here: the person chooses none (U4). A type whose link depends on its entitlement key says so in
/// <paramref name="LinkByKey"/>: the read then leads a key to its page, and any other key to <paramref name="Link"/>.
/// </summary>
public sealed record NotificationKind(
    string Type,
    NotificationBell Bell,
    NotificationLinkKind Link,
    IReadOnlyList<NotificationChannel> Channels,
    IReadOnlyList<NotificationValue> Values,
    bool LinkByKey = false)
{
    /// <summary>The name of the value whose entitlement key decides the link.</summary>
    public const string KeyValue = "keyName";
}
