using System.Text.Json.Serialization;

namespace Billing.Api.Tenant.Subscriptions;

/// <summary>
/// When the move applies (B9): <c>now</c>, with the rest of the period charged today; <c>at_period_end</c>, a lower plan or yearly to
/// monthly; <c>cancellation</c>, the move to the default plan at the period's end.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlanChangeResponseMove>))]
public enum PlanChangeResponseMove
{
    [JsonStringEnumMemberName("now")]
    Now,

    [JsonStringEnumMemberName("at_period_end")]
    AtPeriodEnd,

    [JsonStringEnumMemberName("cancellation")]
    Cancellation,
}
