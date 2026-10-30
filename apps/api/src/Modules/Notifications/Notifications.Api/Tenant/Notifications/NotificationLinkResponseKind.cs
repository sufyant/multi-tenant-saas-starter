using System.Text.Json.Serialization;

namespace Notifications.Api.Tenant.Notifications;

/// <summary>Where a notification leads, which the web turns into a URL.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NotificationLinkResponseKind>))]
public enum NotificationLinkResponseKind
{
    /// <summary>The start page of the tenant web, which opens the reader's one tenant or lets them choose one, without a tenant.</summary>
    [JsonStringEnumMemberName("start")]
    Start,

    /// <summary>The tenant's members.</summary>
    [JsonStringEnumMemberName("tenant_members")]
    TenantMembers,

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
