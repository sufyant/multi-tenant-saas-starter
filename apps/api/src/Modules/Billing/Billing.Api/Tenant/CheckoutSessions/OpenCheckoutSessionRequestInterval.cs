using System.Text.Json.Serialization;

namespace Billing.Api.Tenant.CheckoutSessions;

/// <summary>How often the subscription pays.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OpenCheckoutSessionRequestInterval>))]
public enum OpenCheckoutSessionRequestInterval
{
    [JsonStringEnumMemberName("monthly")]
    Monthly,

    [JsonStringEnumMemberName("yearly")]
    Yearly,
}
