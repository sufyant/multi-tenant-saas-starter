using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.Plans;

/// <summary>What happens when a tenant's use of a meter reaches the plan's quota in a period: the use stops, or the extra is charged.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MeterSettingRequestOverage>))]
public enum MeterSettingRequestOverage
{
    [JsonStringEnumMemberName("stop")]
    Stop,

    [JsonStringEnumMemberName("charge")]
    Charge,
}
