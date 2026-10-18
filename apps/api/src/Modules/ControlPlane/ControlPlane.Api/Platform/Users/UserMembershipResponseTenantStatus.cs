using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.Users;

/// <summary>The status of a tenant a user is a member of: active, or suspended or closed when its members cannot enter it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UserMembershipResponseTenantStatus>))]
public enum UserMembershipResponseTenantStatus
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("suspended")]
    Suspended,

    [JsonStringEnumMemberName("closed")]
    Closed,
}
