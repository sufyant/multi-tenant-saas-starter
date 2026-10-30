namespace Notifications.Api.Tenant.Notifications;

/// <summary>
/// A notification as a bell shows it (U7). Its title and body are rendered in the reader's language (U2). The link names where it leads, as a
/// kind and the tenant it is about: the web builds the URL.
/// </summary>
/// <param name="Id">The notification's id, which opening it names.</param>
/// <param name="Type">The notification's type, from the enum NotificationType.</param>
/// <param name="Category">The category the email preferences name, from the enum NotificationCategory.</param>
/// <param name="Title">The title, in the reader's language.</param>
/// <param name="Body">The body, in the reader's language.</param>
/// <param name="Link">Where it leads.</param>
/// <param name="CreatedAt">When the event it tells of happened.</param>
/// <param name="ReadAt">When the reader opened it; none while it is unread.</param>
public sealed record NotificationResponse(
    Guid Id,
    string Type,
    string Category,
    string Title,
    string Body,
    NotificationLinkResponse Link,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
