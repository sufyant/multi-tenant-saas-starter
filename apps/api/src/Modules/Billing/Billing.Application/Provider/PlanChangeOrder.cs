namespace Billing.Application.Provider;

/// <summary>
/// A member's move of the tenant's subscription to a plan and an interval (B9): the plan's base fee once and its fee per person for each
/// seated member, every interval, in the currency's minor unit, as a checkout's order (B3). A price of 0 is left out.
/// </summary>
public sealed record PlanChangeOrder(
    string SubscriptionId,
    Guid PlanId,
    string PlanName,
    string Currency,
    bool Yearly,
    long BaseAmount,
    long PerPersonAmount,
    int People);
