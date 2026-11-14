using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>What one line of a flag's history tells.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FeatureFlagChangeResponseKind>))]
public enum FeatureFlagChangeResponseKind
{
    [JsonStringEnumMemberName("added_to_code")]
    AddedToCode,

    [JsonStringEnumMemberName("removed_from_code")]
    RemovedFromCode,

    [JsonStringEnumMemberName("listed")]
    Listed,

    [JsonStringEnumMemberName("unlisted")]
    Unlisted,

    [JsonStringEnumMemberName("percentage_changed")]
    PercentageChanged,

    [JsonStringEnumMemberName("everyone_else_changed")]
    EveryoneElseChanged,
}
