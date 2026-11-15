using SharedKernel;

namespace Billing.Application.Subscriptions.CancelSubscription;

/// <summary>Ends the subscription at the provider at once, with no refund and no new invoice: the tenant's purge (B8).</summary>
[MessageName("cancel-subscription")]
public sealed record CancelSubscriptionCommand(string SubscriptionId);
