using System.Text.Json.Serialization;

namespace Billing.Api.Tenant.Subscriptions;

/// <summary>How often the subscription pays.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SubscriptionResponseInterval>))]
public enum SubscriptionResponseInterval
{
    [JsonStringEnumMemberName("monthly")]
    Monthly,

    [JsonStringEnumMemberName("yearly")]
    Yearly,
}
