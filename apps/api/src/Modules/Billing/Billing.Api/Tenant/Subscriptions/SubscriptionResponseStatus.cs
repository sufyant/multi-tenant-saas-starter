using System.Text.Json.Serialization;

namespace Billing.Api.Tenant.Subscriptions;

/// <summary>The subscription's state at the provider (Stripe, "Subscription statuses"). Only an active one keeps the tenant on its plan.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SubscriptionResponseStatus>))]
public enum SubscriptionResponseStatus
{
    [JsonStringEnumMemberName("incomplete")]
    Incomplete,

    [JsonStringEnumMemberName("incomplete_expired")]
    IncompleteExpired,

    [JsonStringEnumMemberName("trialing")]
    Trialing,

    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("past_due")]
    PastDue,

    [JsonStringEnumMemberName("canceled")]
    Canceled,

    [JsonStringEnumMemberName("unpaid")]
    Unpaid,

    [JsonStringEnumMemberName("paused")]
    Paused,
}
