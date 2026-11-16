using System.Text.Json.Serialization;

namespace Billing.Api.Platform.Subscriptions;

/// <summary>How the tenant pays from now on (B12): by card in the app, or by Contract, invoiced outside the app.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SetBilledByRequestBilledBy>))]
public enum SetBilledByRequestBilledBy
{
    [JsonStringEnumMemberName("card")]
    Card,

    [JsonStringEnumMemberName("contract")]
    Contract,
}
