using SharedKernel;

namespace Notifications.Application.Notifications.ListNotifications;

/// <summary>A page of one bell of the reader (U7): the tenant's with the personal ones, the personal ones alone, or the platform's.</summary>
[MessageName("list-notifications")]
public sealed record ListNotificationsQuery(NotificationReader Reader, Inbox Inbox, CursorRequest Page);
