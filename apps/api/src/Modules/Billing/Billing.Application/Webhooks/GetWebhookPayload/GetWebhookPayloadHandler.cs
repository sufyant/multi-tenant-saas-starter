using Billing.Application.Provider;
using SharedKernel;

namespace Billing.Application.Webhooks.GetWebhookPayload;

// A body whose signature is missing, wrong or too old is refused (A5's exception for signed webhooks).
public static class GetWebhookPayloadHandler
{
    public static Result<WebhookEventView> Handle(GetWebhookPayloadQuery query, IBillingProvider provider) =>
        provider.ReadEvent(query.Body, query.Signature) is { } read
            ? new WebhookEventView(read.Id, read.Type, read.SubscriptionId)
            : Errors.WebhookInvalid;
}
