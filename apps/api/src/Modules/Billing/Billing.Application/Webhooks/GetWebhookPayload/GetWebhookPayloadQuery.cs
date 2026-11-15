using SharedKernel;

namespace Billing.Application.Webhooks.GetWebhookPayload;

/// <summary>
/// Reads the provider's event in a webhook's body, after its signature (Stripe, "Webhooks", "Verify webhook signatures"). It reads no
/// network and no database.
/// </summary>
[MessageName("get-webhook-payload")]
public sealed record GetWebhookPayloadQuery(string Body, string? Signature);
