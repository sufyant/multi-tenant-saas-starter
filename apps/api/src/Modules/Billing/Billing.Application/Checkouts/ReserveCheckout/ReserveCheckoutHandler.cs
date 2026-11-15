using Billing.Application.Checkouts.ExpireCheckoutSession;
using Billing.Domain.Checkouts;
using SharedKernel;
using Wolverine;

namespace Billing.Application.Checkouts.ReserveCheckout;

// The tenant's customer is locked first, so two checkouts of one tenant run one after the other, the first one too (K6a). A replaced page
// is expired at the provider through the outbox, in the same transaction, so no replaced page stays payable (B7).
public static class ReserveCheckoutHandler
{
    public static async Task<(CheckoutReservation, OutgoingMessages)> HandleAsync(
        ReserveCheckoutCommand command,
        ICheckoutRepository checkouts,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (await checkouts.FindForUpdateAsync(cancellationToken) is not { } checkout)
        {
            checkout = Checkout.Open(Guid.CreateVersion7(now), command.PlanId, command.Yearly, command.People, now);
            checkouts.Add(checkout);
            await checkouts.SaveChangesAsync(cancellationToken);
            return (new CheckoutReservation(checkout.Key, null), []);
        }

        var (change, replaced) = checkout.Reserve(Guid.CreateVersion7(now), command.PlanId, command.Yearly, command.People, now);
        if (change == StateChange.Unchanged)
        {
            return (new CheckoutReservation(checkout.Key, checkout.Url is { } url ? new Uri(url) : null), []);
        }

        await checkouts.SaveChangesAsync(cancellationToken);
        return (new CheckoutReservation(checkout.Key, null), replaced is null ? [] : [new ExpireCheckoutSessionCommand(replaced)]);
    }
}
