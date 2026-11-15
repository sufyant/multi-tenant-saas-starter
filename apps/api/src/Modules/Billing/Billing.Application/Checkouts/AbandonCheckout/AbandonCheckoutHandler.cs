using Billing.Application.Checkouts.ExpireCheckoutPages;
using Billing.Application.Customers;
using Wolverine;

namespace Billing.Application.Checkouts.AbandonCheckout;

// The tenant's customer is locked first (K6a, B7). A key that another checkout replaced since was handled by that replacement.
public static class AbandonCheckoutHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        AbandonCheckoutCommand command,
        ICustomerStore customers,
        ICheckoutRepository checkouts,
        CancellationToken cancellationToken)
    {
        if (await customers.FindForUpdateAsync(cancellationToken) is not { } customer
            || await checkouts.FindForUpdateAsync(cancellationToken) is not { } checkout || checkout.Key != command.Key)
        {
            return [];
        }

        checkouts.Remove(checkout);
        await checkouts.SaveChangesAsync(cancellationToken);
        return [new ExpireCheckoutPagesCommand(customer.CustomerId, command.Key)];
    }
}
