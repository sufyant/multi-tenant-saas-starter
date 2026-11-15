using System.Text.Json.Serialization;

namespace Billing.Api.Tenant.Subscriptions;

/// <summary>How often the subscription pays after the move.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlanChangeRequestInterval>))]
public enum PlanChangeRequestInterval
{
    [JsonStringEnumMemberName("monthly")]
    Monthly,

    [JsonStringEnumMemberName("yearly")]
    Yearly,
}
