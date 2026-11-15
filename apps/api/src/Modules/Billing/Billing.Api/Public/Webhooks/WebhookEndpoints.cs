using Billing.Application.Subscriptions;
using Billing.Application.Webhooks.GetWebhookPayload;
using Billing.Application.Webhooks.ReceiveWebhook;
using Billing.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Billing.Api.Public.Webhooks;

/// <summary>
/// Stripe's webhook: the one public endpoint that receives something, A5's exception for signed webhooks (step 40). It trusts only a body
/// whose signature is Stripe's, keeps the event by its id and answers at once; the work follows from the outbox (Stripe, "Webhooks",
/// "Respond quickly"; O1).
/// </summary>
public static class WebhookEndpoints
{
    /// <summary>The header that carries Stripe's signature of the body.</summary>
    public const string SignatureHeader = "Stripe-Signature";

    // Only Stripe calls it, so the OpenAPI document of the clients leaves it out. It stays under the general rate limit by address, as every
    // public route is (API4): a 429 is an answer that Stripe tries again, for up to three days (Stripe, "Webhooks", "Retry behavior").
    public static void Map(RouteGroupBuilder publicRoutes) =>
        publicRoutes.MapPost("/billing/webhook", ReceiveWebhook).ExcludeFromDescription();

    // The signature is of the body's exact bytes, so the body is read as text and never bound. An event handled before changes nothing, and
    // the same answer goes back, so Stripe stops sending it: the event's id is the idempotency key (I1).
    private static async Task<Result> ReceiveWebhook(HttpRequest request, BillingOptions billing, IMessageBus bus, CancellationToken cancellationToken)
    {
        if (!billing.Enabled)
        {
            return Errors.BillingOff;
        }

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (await bus.InvokeAsync<WebhookEventView?>(new GetWebhookPayloadQuery(body, request.Headers[SignatureHeader].FirstOrDefault()), cancellationToken) is not { } read)
        {
            return Errors.WebhookInvalid;
        }

        return await bus.InvokeAsync<Result>(new ReceiveWebhookCommand(read.Id, read.Type, read.SubscriptionId), cancellationToken);
    }
}
