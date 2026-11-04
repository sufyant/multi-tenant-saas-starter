using SharedKernel;

namespace Notifications.Application.Notifications.ListNotifications;

/// <summary>
/// A page of one bell of the reader (U7): the tenant's with the personal ones, the personal ones alone, or the platform's; with
/// <paramref name="Unread"/>, only those whose <c>unread</c> has that value (N7h), and without it the whole bell.
/// </summary>
[MessageName("list-notifications")]
public sealed record ListNotificationsQuery(NotificationReader Reader, Inbox Inbox, CursorRequest Page, bool? Unread);
