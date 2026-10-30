namespace Notifications.Domain.Notifications;

/// <summary>
/// Where a notification leads, as a kind and the notification's tenant, never a URL: the web builds the URL (U1). The kinds are the API's
/// names; the OpenAPI document lists them as an enum of the response.
/// </summary>
public enum NotificationLinkKind
{
    /// <summary>Nowhere: the person cannot enter what it is about.</summary>
    None,

    /// <summary>The tenant's home in the tenant web.</summary>
    Tenant,

    /// <summary>The tenant's members.</summary>
    TenantMembers,

    /// <summary>The tenant's plan.</summary>
    TenantPlan,

    /// <summary>The tenant's page in the platform.</summary>
    PlatformTenant,
}
