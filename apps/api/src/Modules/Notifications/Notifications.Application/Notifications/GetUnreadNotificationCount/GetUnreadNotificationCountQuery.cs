using SharedKernel;

namespace Notifications.Application.Notifications.GetUnreadNotificationCount;

/// <summary>How many notifications of one bell the reader has not opened (U7).</summary>
[MessageName("get-unread-notification-count")]
public sealed record GetUnreadNotificationCountQuery(NotificationReader Reader, Inbox Inbox);
