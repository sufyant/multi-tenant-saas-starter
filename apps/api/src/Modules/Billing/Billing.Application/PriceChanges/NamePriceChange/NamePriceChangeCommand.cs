using Billing.Contracts.PriceChanges;
using SharedKernel;

namespace Billing.Application.PriceChanges.NamePriceChange;

/// <summary>
/// The second step of a tenant's notice of a price change (B13): it names the tenant through ControlPlane's contracts, on ControlPlane's
/// DbContext, as one DbContext owns a handler's transaction (W1).
/// </summary>
[MessageName("name-price-change")]
public sealed record NamePriceChangeCommand(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid PlanId,
    string PlanName,
    string Currency,
    bool Yearly,
    PriceChangePricesRecord OldPrices,
    PriceChangePricesRecord NewPrices,
    DateTimeOffset MovesAt);
