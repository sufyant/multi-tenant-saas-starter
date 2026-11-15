namespace Billing.Application.Provider;

/// <summary>
/// What a move at once costs today (10.04): the prorated amount without tax, in the currency's minor unit, for the moment of
/// <paramref name="ProratedAt"/>, which the move then names so that it charges the same amount (Stripe, "Prorations", "Preview").
/// </summary>
public sealed record PlanChangePreview(long AmountDue, DateTimeOffset ProratedAt);
