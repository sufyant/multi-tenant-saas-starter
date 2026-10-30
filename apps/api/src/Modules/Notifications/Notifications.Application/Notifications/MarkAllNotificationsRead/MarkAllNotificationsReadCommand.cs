using SharedKernel;

namespace Notifications.Application.Notifications.MarkAllNotificationsRead;

/// <summary>The reader marks everything one bell shows read, and nothing of another tenant (U7).</summary>
[MessageName("mark-all-notifications-read")]
public sealed record MarkAllNotificationsReadCommand(NotificationReader Reader, Inbox Inbox);
