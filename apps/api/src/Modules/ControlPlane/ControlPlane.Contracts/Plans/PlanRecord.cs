namespace ControlPlane.Contracts.Plans;

/// <summary>
/// A plan as billing reads it: its prices are whole numbers of the minor unit of <paramref name="Currency"/>, an ISO 4217 code. A move to the
/// default plan ends the tenant's subscription (step 40).
/// </summary>
public sealed record PlanRecord(Guid Id, string Name, string Currency, PlanPricesRecord Prices, bool IsDefault);
