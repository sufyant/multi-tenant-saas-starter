namespace Billing.Api.Tenant.CheckoutSessions;

/// <summary>
/// The provider's page where the person pays by card. It works for a short time and is never stored; the page comes back with
/// <c>checkout=success</c> or <c>checkout=cancelled</c> after the address of the request.
/// </summary>
public sealed record CheckoutSessionResponse(Uri Url);
