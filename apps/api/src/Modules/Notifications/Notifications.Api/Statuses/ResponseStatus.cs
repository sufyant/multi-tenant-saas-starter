using Notifications.Api.Tenant.Notifications;

namespace Notifications.Api.Statuses;

/// <summary>Maps each of Notifications' kinds to the kind enum of a response (N7r). A kind a response cannot carry is a fault, not an answer.</summary>
internal static class ResponseStatus
{
    public static NotificationLinkResponseKind OfLink(string kind) => kind switch
    {
        "None" => NotificationLinkResponseKind.None,
        "Tenant" => NotificationLinkResponseKind.Tenant,
        "TenantMembers" => NotificationLinkResponseKind.TenantMembers,
        "TenantPlan" => NotificationLinkResponseKind.TenantPlan,
        "TenantRoles" => NotificationLinkResponseKind.TenantRoles,
        "TenantSettings" => NotificationLinkResponseKind.TenantSettings,
        "PlatformTenant" => NotificationLinkResponseKind.PlatformTenant,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The response has no value for this kind of Notifications."),
    };
}
