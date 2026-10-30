using JasperFx;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Notifications.Api.Authentication;
using Notifications.Api.Statuses;
using Notifications.Application.Notifications;
using Notifications.Application.Notifications.GetNotificationReader;
using Notifications.Application.Notifications.GetUnreadNotificationCount;
using Notifications.Application.Notifications.ListNotifications;
using Notifications.Application.Notifications.MarkAllNotificationsRead;
using SharedKernel;
using Wolverine;

namespace Notifications.Api.Tenant.Notifications;

/// <summary>
/// The tenant web's bell of the tenant in the route (U7): the tenant's notifications and the reader's personal ones. Every member reads it,
/// and no plan gates it (E7). The read declares the reader, never the tenant: the tenant door has proved the membership, and the route's
/// tenant only selects the reader's own rows.
/// </summary>
public static class NotificationEndpoints
{
    // Wolverine's value for a message without a tenant (W3): the reader's transaction declares the reader alone (R4).
    private const string WithoutTenant = StorageConstants.DefaultTenantId;

    public static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapGet("/notifications", ListNotifications).RequireAuthorization(NotificationPermissions.TenantBell).WithMetadata(new NotPlanGated());
        tenant.MapGet("/notifications/unread-count", GetUnreadNotificationCount)
            .RequireAuthorization(NotificationPermissions.TenantBell)
            .WithMetadata(new NotPlanGated());
        tenant.MapPost("/notifications/read", MarkAllNotificationsRead)
            .RequireAuthorization(NotificationPermissions.TenantBell)
            .WithMetadata(new NotPlanGated());
    }

    internal static NotificationResponse ToResponse(NotificationView notification) => new(
        notification.Id,
        notification.Type,
        notification.Category,
        notification.Title,
        notification.Body,
        new NotificationLinkResponse(ResponseStatus.OfLink(notification.Link), notification.TenantId),
        notification.CreatedAt,
        notification.ReadAt);

    // The reader's notifications, as the reader's own transaction reads them: the endpoint sends its queries with no tenant (U7).
    internal static async Task<Result<CursorPage<NotificationResponse>>> ListAsync(
        HttpContext http, IMessageBus bus, Inbox inbox, string? cursor, int? limit, CancellationToken cancellationToken)
    {
        var page = CursorRequest.Create(cursor, limit);
        if (!page.IsSuccess)
        {
            return page.Error;
        }

        var reader = await ReaderAsync(http, bus, cancellationToken);
        var found = await bus.InvokeForTenantAsync<Result<CursorPage<NotificationView>>>(
            WithoutTenant, new ListNotificationsQuery(reader, inbox, page.Value), cancellationToken);
        return found.Map(notifications => notifications.Map(ToResponse));
    }

    internal static async Task<Result<UnreadNotificationCountResponse>> CountAsync(HttpContext http, IMessageBus bus, Inbox inbox, CancellationToken cancellationToken)
    {
        var reader = await ReaderAsync(http, bus, cancellationToken);
        var unread = await bus.InvokeForTenantAsync<int>(WithoutTenant, new GetUnreadNotificationCountQuery(reader, inbox), cancellationToken);
        return new UnreadNotificationCountResponse(unread);
    }

    internal static async Task<Result> MarkAllReadAsync(HttpContext http, IMessageBus bus, Inbox inbox, CancellationToken cancellationToken)
    {
        var reader = await ReaderAsync(http, bus, cancellationToken);
        return await bus.InvokeForTenantAsync<Result>(WithoutTenant, new MarkAllNotificationsReadCommand(reader, inbox), cancellationToken);
    }

    internal static Task<NotificationReader> ReaderAsync(HttpContext http, IMessageBus bus, CancellationToken cancellationToken) =>
        bus.InvokeForTenantAsync<NotificationReader>(WithoutTenant, new GetNotificationReaderQuery(http.User.Id()), cancellationToken);

    private static Task<Result<CursorPage<NotificationResponse>>> ListNotifications(
        Guid tenantId, string? cursor, int? limit, HttpContext http, IMessageBus bus, CancellationToken cancellationToken) =>
        ListAsync(http, bus, Inbox.OfTenant(tenantId), cursor, limit, cancellationToken);

    private static Task<Result<UnreadNotificationCountResponse>> GetUnreadNotificationCount(
        Guid tenantId, HttpContext http, IMessageBus bus, CancellationToken cancellationToken) =>
        CountAsync(http, bus, Inbox.OfTenant(tenantId), cancellationToken);

    private static Task<Result> MarkAllNotificationsRead(Guid tenantId, HttpContext http, IMessageBus bus, CancellationToken cancellationToken) =>
        MarkAllReadAsync(http, bus, Inbox.OfTenant(tenantId), cancellationToken);
}
