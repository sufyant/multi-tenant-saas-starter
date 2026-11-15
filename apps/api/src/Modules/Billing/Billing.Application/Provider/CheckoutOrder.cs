namespace Billing.Application.Provider;

/// <summary>
/// What a checkout pays for: the plan's base fee once and its fee per person for each seated member, every interval, in the currency's minor
/// unit. A price of 0 is left out. The tenant's customer pays; the tenant and the member who pays go into the metadata for support only (B5).
/// </summary>
public sealed record CheckoutOrder(
    Guid Key,
    DateTimeOffset ExpiresAt,
    Guid TenantId,
    Guid PaidBy,
    string CustomerId,
    Guid PlanId,
    string PlanName,
    string Currency,
    bool Yearly,
    long BaseAmount,
    long PerPersonAmount,
    int People,
    Uri SuccessUrl,
    Uri CancelUrl);
