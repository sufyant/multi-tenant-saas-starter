using Billing.Application.Provider;

namespace Billing.Application.Webhooks.GetWebhookPayload;

// None for a body whose signature is missing, wrong or too old (A5's exception for signed webhooks). It answers no Result: a handler that does
// is under the idempotency layer's transaction (I4), and this one takes no transaction, as it reads the provider's adapter.
public static class GetWebhookPayloadHandler
{
    public static WebhookEventView? Handle(GetWebhookPayloadQuery query, IBillingProvider provider) =>
        provider.ReadEvent(query.Body, query.Signature) is { } read ? new WebhookEventView(read.Id, read.Type, read.SubscriptionId) : null;
}
