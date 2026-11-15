namespace Billing.Application.CheckoutSessions.GetCheckoutFacts;

/// <summary>
/// The facts of a checkout or a plan change: the member who pays and their address, the plan, its prices and whether it is the default
/// plan, and the seated members.
/// </summary>
public sealed record CheckoutFacts(
    Guid PaidBy,
    string? Email,
    Guid PlanId,
    string PlanName,
    string Currency,
    long BaseMonthly,
    long PerPersonMonthly,
    long BaseYearly,
    long PerPersonYearly,
    int SeatedMembers,
    bool IsDefault);
