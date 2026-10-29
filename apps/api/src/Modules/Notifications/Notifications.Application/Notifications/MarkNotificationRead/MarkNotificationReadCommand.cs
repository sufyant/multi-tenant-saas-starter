using SharedKernel;

namespace Notifications.Application.Notifications.MarkNotificationRead;

/// <summary>The reader opened one of their notifications, of any bell (U7).</summary>
[MessageName("mark-notification-read")]
public sealed record MarkNotificationReadCommand(NotificationReader Reader, Guid NotificationId);
