namespace Notifications.Application.Notifications;

/// <summary>
/// A notification as a bell shows it (U7): its type and category, its title and body rendered in the reader's language (U2), where it leads,
/// by the name of its link kind, the tenant it is about or none, when it came and when the reader opened it.
/// </summary>
public sealed record NotificationView(
    Guid Id,
    string Type,
    string Category,
    string Title,
    string Body,
    string Link,
    Guid? TenantId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
