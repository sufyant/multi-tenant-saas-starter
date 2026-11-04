namespace Notifications.Application.Notifications;

/// <summary>
/// A notification as a bell shows it (U7): its type, its title and body rendered in the reader's language (U2), its area in that
/// language, the person who acted, named now, or none, the tenant's name as it was or none, where it leads, by the name of its link kind,
/// the tenant it is about or none, when it came, when the reader opened it, and whether it is unread, which follows from that.
/// </summary>
public sealed record NotificationView(
    Guid Id,
    string Type,
    string Title,
    string Body,
    string Area,
    string? ActorName,
    string? TenantName,
    string Link,
    Guid? TenantId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt)
{
    /// <summary>Whether the reader has not opened it yet: derived from <see cref="ReadAt"/>, never stored.</summary>
    public bool Unread => ReadAt is null;
}
