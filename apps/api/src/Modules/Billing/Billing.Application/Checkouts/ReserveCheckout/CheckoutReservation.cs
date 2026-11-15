namespace Billing.Application.Checkouts.ReserveCheckout;

/// <summary>
/// The provider's idempotency key of the tenant's checkout, the moment its page stops working, and its page when the provider made it
/// already. Every call of the key asks the provider the same, so it answers the same page (Stripe, "Idempotent requests").
/// </summary>
public sealed record CheckoutReservation(Guid Key, DateTimeOffset ExpiresAt, Uri? Url);
