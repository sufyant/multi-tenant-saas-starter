namespace Billing.Application.Webhooks.GetWebhookPayload;

/// <summary>A signed event: its id, its type, and the subscription it is about, when it is about one.</summary>
public sealed record WebhookEventView(string Id, string Type, string? SubscriptionId);
