using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.Tenants;

/// <summary>The status of a tenant: provisioning until its creation ends, then active, suspended or closed, or cancelled.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TenantResponseStatus>))]
public enum TenantResponseStatus
{
    [JsonStringEnumMemberName("provisioning")]
    Provisioning,

    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("suspended")]
    Suspended,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled,

    [JsonStringEnumMemberName("closed")]
    Closed,
}
