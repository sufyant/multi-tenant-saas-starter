using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>The first rule that turns a flag on for an organisation: it is listed, it is in the percentage, or the flag is on for everyone else.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TenantFeatureFlagResponseReason>))]
public enum TenantFeatureFlagResponseReason
{
    [JsonStringEnumMemberName("listed")]
    Listed,

    [JsonStringEnumMemberName("percentage")]
    Percentage,

    [JsonStringEnumMemberName("everyone")]
    Everyone,
}
