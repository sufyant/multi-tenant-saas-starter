using SharedKernel;

namespace Billing.Application.Subscriptions.SyncSubscription;

/// <summary>
/// Reads a subscription from the provider as it is now, and applies it in the tenant it names. The state comes from the provider, never
/// from an event's body, so events that arrive out of order still end in the provider's state (Stripe, "Webhooks", "Event ordering").
/// </summary>
[MessageName("sync-subscription")]
public sealed record SyncSubscriptionCommand(string SubscriptionId);
