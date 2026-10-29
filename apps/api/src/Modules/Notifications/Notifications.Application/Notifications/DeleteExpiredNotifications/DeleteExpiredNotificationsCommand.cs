using SharedKernel;

namespace Notifications.Application.Notifications.DeleteExpiredNotifications;

/// <summary>Deletes a batch of the notifications older than the retention, of every user (U8). The worker sends it every hour.</summary>
[MessageName("delete-expired-notifications")]
public sealed record DeleteExpiredNotificationsCommand;
