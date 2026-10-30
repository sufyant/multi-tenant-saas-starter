using ControlPlane.Contracts.Plans;
using Notifications.Domain.Notifications;

namespace Notifications.Application.Notifications;

/// <summary>
/// Where a notification leads (U1): its type's link kind, or, for a type whose link depends on its entitlement key, the page of the key:
/// the members for <c>members.max</c>, the roles for <c>roles.custom</c> and <c>roles.custom.max</c>, the tenant's general settings for any
/// other key. The keys are ControlPlane's generated constants (D1).
/// </summary>
public static class NotificationLinks
{
    private static readonly Dictionary<string, NotificationLinkKind> ByKey = new(StringComparer.Ordinal)
    {
        [Limits.MembersMax] = NotificationLinkKind.TenantMembers,
        [Features.RolesCustom] = NotificationLinkKind.TenantRoles,
        [Limits.RolesCustomMax] = NotificationLinkKind.TenantRoles,
    };

    public static NotificationLinkKind Of(NotificationKind kind, IReadOnlyDictionary<string, string> values) =>
        kind.LinkByKey && values.TryGetValue(NotificationKind.KeyValue, out var key) && ByKey.TryGetValue(key, out var link) ? link : kind.Link;
}
