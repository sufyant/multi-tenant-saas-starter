using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>A flag's removal from the code: in use, no cleanup yet, ready to remove, a release flag on for everyone for 30 days, or unused, off for every organisation for 30 days.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FeatureFlagResponseCleanup>))]
public enum FeatureFlagResponseCleanup
{
    [JsonStringEnumMemberName("in_use")]
    InUse,

    [JsonStringEnumMemberName("ready_to_remove")]
    ReadyToRemove,

    [JsonStringEnumMemberName("unused")]
    Unused,
}
