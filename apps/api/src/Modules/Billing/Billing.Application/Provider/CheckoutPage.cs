namespace Billing.Application.Provider;

/// <summary>A checkout's page at the provider: its id, its address, and when it stops working.</summary>
public sealed record CheckoutPage(string SessionId, Uri Url, DateTimeOffset ExpiresAt);
