using Billing.Application.Checkouts.ExpireOtherCheckouts;
using Billing.Application.Customers;
using Billing.Domain.Checkouts;
using SharedKernel;
using Wolverine;

namespace Billing.Application.Checkouts.ReserveCheckout;

// The tenant's customer, the root of its billing, is locked first, so two checkouts of one tenant run one after the other, the first one
// too (K6a, B7). A closed tenant opens no checkout (B8). A new key expires every other open page of the customer through the outbox, in the
// same transaction, so no replaced page stays payable. Rejections come first (W7).
public static class ReserveCheckoutHandler
{
    public static async Task<(Result<CheckoutReservation>, OutgoingMessages)> HandleAsync(
        ReserveCheckoutCommand command,
        ICustomerStore customers,
        ICheckoutRepository checkouts,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var customer = await customers.FindForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The endpoint keeps the tenant's customer before its checkout.");
        if (customer.ClosedAt is not null)
        {
            return (Errors.TenantClosed, []);
        }

        var now = time.GetUtcNow();
        var order = new CheckoutOrderOf(command.PlanId, command.Yearly, command.People, command.ReturnUrl.ToString());
        if (await checkouts.FindForUpdateAsync(cancellationToken) is not { } checkout)
        {
            checkout = Checkout.Open(Guid.CreateVersion7(now), order, now);
            checkouts.Add(checkout);
        }
        else if (checkout.Reserve(Guid.CreateVersion7(now), order, now) == StateChange.Unchanged)
        {
            return (new CheckoutReservation(checkout.Key, checkout.ExpiresAt, checkout.Url is { } url ? new Uri(url) : null), []);
        }

        await checkouts.SaveChangesAsync(cancellationToken);
        return (new CheckoutReservation(checkout.Key, checkout.ExpiresAt, null), [new ExpireOtherCheckoutsCommand(customer.CustomerId, checkout.Key)]);
    }
}
