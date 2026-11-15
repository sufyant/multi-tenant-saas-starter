namespace Billing.Application.CheckoutSessions.GetCheckoutFacts;

/// <summary>The facts of a checkout: the member who pays and their address, the plan and its prices, and the seated members.</summary>
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
    int SeatedMembers);
