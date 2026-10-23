using System.Text.Json.Serialization;

namespace ControlPlane.Api.Me.Tenants;

/// <summary>The status of a tenant as its members see it: active, or suspended, closed or purging when they cannot enter it. A purged tenant has no member.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MyTenantResponseStatus>))]
public enum MyTenantResponseStatus
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("suspended")]
    Suspended,

    [JsonStringEnumMemberName("closed")]
    Closed,

    [JsonStringEnumMemberName("purging")]
    Purging,
}
