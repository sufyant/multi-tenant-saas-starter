namespace Billing.Application.Provider;

/// <summary>
/// A subscription of ours at the provider: the tenant it names, the member who paid, the plan it pays for, and its state. The status is the
/// name of a <c>SubscriptionStatus</c> (N7r), which the adapter maps from the provider's own word.
/// </summary>
public sealed record ProviderSubscription(
    Guid TenantId,
    Guid PaidBy,
    string CustomerId,
    string SubscriptionId,
    Guid PlanId,
    bool Yearly,
    string Status,
    DateTimeOffset CurrentPeriodEnd,
    bool CancelAtPeriodEnd);
