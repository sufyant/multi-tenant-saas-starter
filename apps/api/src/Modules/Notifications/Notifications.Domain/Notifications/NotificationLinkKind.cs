namespace Notifications.Domain.Notifications;

/// <summary>
/// Where a notification leads, as a kind and the notification's tenant, never a URL: the web builds the URL (U1). The kinds are the API's
/// names; the OpenAPI document lists them as an enum of the response.
/// </summary>
public enum NotificationLinkKind
{
    /// <summary>The start page of the tenant web, which opens the reader's one tenant or lets them choose one, without a tenant.</summary>
    Start,

    /// <summary>The tenant's members.</summary>
    TenantMembers,

    /// <summary>The tenant's roles.</summary>
    TenantRoles,

    /// <summary>The tenant's general settings.</summary>
    TenantSettings,

    /// <summary>The tenant's page in the platform.</summary>
    PlatformTenant,
}
