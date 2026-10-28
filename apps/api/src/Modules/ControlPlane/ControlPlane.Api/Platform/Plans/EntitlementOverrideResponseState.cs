using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// The state of an override at the moment of the request: active, or expired, when its expiry has passed. An expired override applies no
/// more, and stays until a system admin removes it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EntitlementOverrideResponseState>))]
public enum EntitlementOverrideResponseState
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("expired")]
    Expired,
}
