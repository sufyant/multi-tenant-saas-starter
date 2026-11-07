namespace Notifications.Domain.Notifications;

/// <summary>
/// One type of the catalog (U1): the bell that shows it, where it leads, its area, the channels it goes on, and the values its texts use.
/// The product fixes the channels here: the person chooses none (U4). The area is a word of <c>notificationWords.area</c>, which the list
/// shows in the reader's language; it is a label only. <paramref name="Actor"/> names the person value of the one who acted, none for an
/// event the platform did. A type whose link depends on its entitlement key says so in <paramref name="LinkByKey"/>: the read then leads a
/// key to its page, and any other key to <paramref name="Link"/>.
/// </summary>
public sealed record NotificationKind(
    string Type,
    NotificationBell Bell,
    NotificationLinkKind Link,
    string Area,
    IReadOnlyList<NotificationChannel> Channels,
    IReadOnlyList<NotificationValue> Values,
    string? Actor = null,
    bool LinkByKey = false)
{
    /// <summary>The name of the value whose entitlement key decides the link.</summary>
    public const string KeyValue = "keyName";

    /// <summary>The name of the value that keeps the tenant's name as it was at that moment.</summary>
    public const string TenantValue = "tenantName";

    /// <summary>Whether the link leads to a page of the notification's tenant, which the web finds by the tenant's id.</summary>
    public bool LeadsIntoATenant => Link is NotificationLinkKind.TenantMembers or NotificationLinkKind.TenantRoles or NotificationLinkKind.TenantSettings;
}
