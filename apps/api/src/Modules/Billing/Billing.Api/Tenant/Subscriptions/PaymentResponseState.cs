using System.Text.Json.Serialization;

namespace Billing.Api.Tenant.Subscriptions;

/// <summary>Where a failed payment stands (B11): Stripe retries it, billing's grace runs, or the tenant moved to the default plan.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PaymentResponseState>))]
public enum PaymentResponseState
{
    [JsonStringEnumMemberName("retrying")]
    Retrying,

    [JsonStringEnumMemberName("grace")]
    Grace,

    [JsonStringEnumMemberName("moved_to_default")]
    MovedToDefault,
}
