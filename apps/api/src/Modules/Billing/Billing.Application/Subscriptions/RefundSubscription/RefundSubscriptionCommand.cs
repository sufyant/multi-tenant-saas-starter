using SharedKernel;

namespace Billing.Application.Subscriptions.RefundSubscription;

/// <summary>
/// Asks the provider to end a second subscription of the tenant at once and to refund what it paid (B7): a page that a race left payable let
/// it through while the first had not ended, and the tenant keeps the first.
/// </summary>
[MessageName("refund-subscription")]
public sealed record RefundSubscriptionCommand(string SubscriptionId);
