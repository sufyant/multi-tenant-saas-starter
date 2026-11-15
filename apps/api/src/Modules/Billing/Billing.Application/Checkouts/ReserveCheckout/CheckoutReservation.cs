namespace Billing.Application.Checkouts.ReserveCheckout;

/// <summary>The provider's idempotency key of the tenant's checkout, and its page when the provider made it already.</summary>
public sealed record CheckoutReservation(Guid Key, Uri? Url);
