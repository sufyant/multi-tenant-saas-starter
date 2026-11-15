using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.Plans;

/// <summary>What happens when a tenant's use of a meter reaches the plan's quota in a period: the use stops, or the extra is charged.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MeterSettingResponseOverage>))]
public enum MeterSettingResponseOverage
{
    [JsonStringEnumMemberName("stop")]
    Stop,

    [JsonStringEnumMemberName("charge")]
    Charge,
}
