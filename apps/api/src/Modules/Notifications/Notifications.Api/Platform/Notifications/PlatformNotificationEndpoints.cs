using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Notifications.Api.Tenant.Notifications;
using Notifications.Application.Notifications;
using SharedKernel;
using Wolverine;

namespace Notifications.Api.Platform.Notifications;

/// <summary>The platform's bell (U7): the notifications of a system admin about the tenants. Every platform notification is about a tenant.</summary>
public static class PlatformNotificationEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapGet("/notifications", ListPlatformNotifications).RequireAuthorization(NotificationPermissions.PlatformBell);
        platform.MapGet("/notifications/unread-count", GetPlatformUnreadNotificationCount).RequireAuthorization(NotificationPermissions.PlatformBell);
        platform.MapPost("/notifications/read", MarkAllPlatformNotificationsRead).RequireAuthorization(NotificationPermissions.PlatformBell);
    }

    private static Task<Result<CursorPage<NotificationResponse>>> ListPlatformNotifications(
        string? cursor, int? limit, HttpContext http, IMessageBus bus, CancellationToken cancellationToken) =>
        NotificationEndpoints.ListAsync(http, bus, Inbox.Platform, cursor, limit, cancellationToken);

    private static Task<Result<UnreadNotificationCountResponse>> GetPlatformUnreadNotificationCount(HttpContext http, IMessageBus bus, CancellationToken cancellationToken) =>
        NotificationEndpoints.CountAsync(http, bus, Inbox.Platform, cancellationToken);

    private static Task<Result> MarkAllPlatformNotificationsRead(HttpContext http, IMessageBus bus, CancellationToken cancellationToken) =>
        NotificationEndpoints.MarkAllReadAsync(http, bus, Inbox.Platform, cancellationToken);
}
