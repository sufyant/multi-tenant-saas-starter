using SharedKernel;

namespace Billing.Application.Webhooks.ReceiveWebhook;

/// <summary>
/// Keeps a signed event of the provider by its id, and asks for the sync of its subscription once: an event that arrives again changes
/// nothing (Stripe, "Webhooks", "Handle duplicate events"). The sync goes through the outbox in the same transaction, so a received event is
/// never lost (O1).
/// </summary>
[MessageName("receive-webhook")]
public sealed record ReceiveWebhookCommand(string Id, string Type, string? SubscriptionId);
