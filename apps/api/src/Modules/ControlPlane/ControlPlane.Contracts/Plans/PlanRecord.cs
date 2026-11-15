namespace ControlPlane.Contracts.Plans;

/// <summary>A plan as billing reads it: its prices are whole numbers of the minor unit of <paramref name="Currency"/>, an ISO 4217 code.</summary>
public sealed record PlanRecord(Guid Id, string Name, bool IsDefault, string Currency, PlanPricesRecord Prices);
