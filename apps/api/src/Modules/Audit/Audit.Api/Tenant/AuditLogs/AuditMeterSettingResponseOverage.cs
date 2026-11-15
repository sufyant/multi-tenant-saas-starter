using System.Text.Json.Serialization;

namespace Audit.Api.Tenant.AuditLogs;

/// <summary>What happens when a tenant's use of a meter reaches the plan's quota in a period: the use stops, or the extra is charged.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditMeterSettingResponseOverage>))]
public enum AuditMeterSettingResponseOverage
{
    [JsonStringEnumMemberName("stop")]
    Stop,

    [JsonStringEnumMemberName("charge")]
    Charge,
}
