namespace Billing.Api.Tenant.Subscriptions;

/// <summary>
/// What a move does (10.04, 10.06): when it applies, at <c>at</c>; what the new plan costs each interval for the seated members,
/// <c>amount</c>, without tax, in the minor unit of <c>currency</c>; and for a move at once, what the rest of the period costs today,
/// <c>amountToday</c>, without tax, for the moment <c>proratedAt</c>, which the move names to charge the same amount. Both are null for a
/// move at the period's end.
/// </summary>
public sealed record PlanChangePreviewResponse(
    PlanChangeResponseMove Move,
    DateTimeOffset At,
    string Currency,
    long Amount,
    long? AmountToday,
    DateTimeOffset? ProratedAt);
