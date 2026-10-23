using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.Tenants;

/// <summary>The status of a tenant: active from its creation, suspended, closed, purging once its purge has started, or purged (K4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TenantResponseStatus>))]
public enum TenantResponseStatus
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("suspended")]
    Suspended,

    [JsonStringEnumMemberName("closed")]
    Closed,

    [JsonStringEnumMemberName("purging")]
    Purging,

    [JsonStringEnumMemberName("purged")]
    Purged,
}
