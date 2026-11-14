using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>What a flag is for: a release flag, or a kill switch.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FeatureFlagResponseType>))]
public enum FeatureFlagResponseType
{
    [JsonStringEnumMemberName("release")]
    Release,

    [JsonStringEnumMemberName("kill_switch")]
    KillSwitch,
}
