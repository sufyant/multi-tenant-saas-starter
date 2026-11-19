using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.Tenants;

/// <summary>A billing situation that the Organisations list filters by (4.38): a payment that is overdue, or Contract (B11, B12).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TenantResponseBilling>))]
public enum TenantResponseBilling
{
    [JsonStringEnumMemberName("payment_overdue")]
    PaymentOverdue,

    [JsonStringEnumMemberName("contract")]
    Contract,
}
