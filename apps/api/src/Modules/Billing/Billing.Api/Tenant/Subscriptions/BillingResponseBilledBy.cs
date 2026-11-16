using System.Text.Json.Serialization;

namespace Billing.Api.Tenant.Subscriptions;

/// <summary>How the tenant pays (B12): by card in the app, or by Contract, invoiced outside the app, as a system admin set it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BillingResponseBilledBy>))]
public enum BillingResponseBilledBy
{
    [JsonStringEnumMemberName("card")]
    Card,

    [JsonStringEnumMemberName("contract")]
    Contract,
}
