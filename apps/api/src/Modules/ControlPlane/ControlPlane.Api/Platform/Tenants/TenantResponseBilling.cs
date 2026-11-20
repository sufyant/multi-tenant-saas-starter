using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// A billing situation that the Organisations list filters by (4.38): a payment that is overdue, Contract (B11, B12), or a running trial
/// (4.39, step 40d).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TenantResponseBilling>))]
public enum TenantResponseBilling
{
    [JsonStringEnumMemberName("payment_overdue")]
    PaymentOverdue,

    [JsonStringEnumMemberName("contract")]
    Contract,

    [JsonStringEnumMemberName("in_trial")]
    InTrial,
}
