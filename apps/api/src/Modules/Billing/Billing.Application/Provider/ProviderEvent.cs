namespace Billing.Application.Provider;

/// <summary>A signed event of the provider: its id, its type, and the subscription it is about, when it is about one.</summary>
public sealed record ProviderEvent(string Id, string Type, string? SubscriptionId);
