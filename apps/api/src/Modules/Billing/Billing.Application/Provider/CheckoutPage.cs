namespace Billing.Application.Provider;

/// <summary>A checkout's page at the provider: its id and its address.</summary>
public sealed record CheckoutPage(string SessionId, Uri Url);
