using System.Text.Json.Serialization;

namespace Notifications.Api.Tenant.Notifications;

/// <summary>Where a notification leads, which the web turns into a URL.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NotificationLinkResponseKind>))]
public enum NotificationLinkResponseKind
{
    /// <summary>Nowhere: the reader cannot enter what it is about.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>The tenant's home in the tenant web.</summary>
    [JsonStringEnumMemberName("tenant")]
    Tenant,

    /// <summary>The tenant's members.</summary>
    [JsonStringEnumMemberName("tenant_members")]
    TenantMembers,

    /// <summary>The tenant's plan.</summary>
    [JsonStringEnumMemberName("tenant_plan")]
    TenantPlan,

    /// <summary>The tenant's roles.</summary>
    [JsonStringEnumMemberName("tenant_roles")]
    TenantRoles,

    /// <summary>The tenant's general settings.</summary>
    [JsonStringEnumMemberName("tenant_settings")]
    TenantSettings,

    /// <summary>The tenant's page in the platform.</summary>
    [JsonStringEnumMemberName("platform_tenant")]
    PlatformTenant,
}
