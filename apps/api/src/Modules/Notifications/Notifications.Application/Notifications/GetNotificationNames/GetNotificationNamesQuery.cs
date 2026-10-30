using SharedKernel;

namespace Notifications.Application.Notifications.GetNotificationNames;

/// <summary>
/// A page of a bell, rendered in the reader's language and time zone, with each person of its values named now by ControlPlane, in one
/// call for the page (U2).
/// </summary>
[MessageName("get-notification-names")]
public sealed record GetNotificationNamesQuery(CursorPage<StoredNotification> Page, NotificationReader Reader);
