using Notifications.Domain.Notifications;

namespace Notifications.Application.Notifications;

/// <summary>
/// What one bell shows of a user's notifications (U7): a tenant's bell shows that tenant's and the personal ones, a signed-in user without a
/// tenant the personal ones, and the platform's bell the platform's. The tenant is a filter of the user's own rows, never a tenant the
/// read declares: the tenant door has proved the membership.
/// </summary>
public sealed record Inbox(NotificationBell Bell, Guid? TenantId)
{
    public static Inbox Personal { get; } = new(NotificationBell.Personal, null);

    public static Inbox Platform { get; } = new(NotificationBell.Platform, null);

    public static Inbox OfTenant(Guid tenantId) => new(NotificationBell.Tenant, tenantId);
}
