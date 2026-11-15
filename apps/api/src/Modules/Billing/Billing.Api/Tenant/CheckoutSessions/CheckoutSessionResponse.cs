namespace Billing.Api.Tenant.CheckoutSessions;

/// <summary>
/// The provider's page of the tenant's one open checkout, where the person pays by card, for an hour; the page comes back with
/// <c>checkout=success</c> or <c>checkout=cancelled</c> after the address of the request.
/// </summary>
public sealed record CheckoutSessionResponse(Uri Url);
