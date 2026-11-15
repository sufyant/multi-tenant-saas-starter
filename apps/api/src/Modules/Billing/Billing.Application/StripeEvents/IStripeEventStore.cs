using Billing.Domain.StripeEvents;

namespace Billing.Application.StripeEvents;

/// <summary>The provider's events that billing received. They belong to no tenant (R6).</summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface IStripeEventStore
{
    /// <summary>Keeps the event; false when an event with its id is kept already, so a second delivery does nothing.</summary>
    internal Task<bool> AddAsync(StripeEvent stripeEvent, CancellationToken cancellationToken);
}
