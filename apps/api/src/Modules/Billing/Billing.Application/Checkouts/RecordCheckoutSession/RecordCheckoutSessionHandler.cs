using Billing.Application.Checkouts.ExpireOtherCheckouts;
using Billing.Application.Customers;
using Wolverine;

namespace Billing.Application.Checkouts.RecordCheckoutSession;

// The tenant's customer is locked first (K6a, B7). A page of a replaced key may have been made after the replacement expired the customer's
// open pages, so it is expired here, but the page of the checkout that replaced it.
public static class RecordCheckoutSessionHandler
{
    public static async Task<(bool, OutgoingMessages)> HandleAsync(
        RecordCheckoutSessionCommand command,
        ICustomerStore customers,
        ICheckoutRepository checkouts,
        CancellationToken cancellationToken)
    {
        var customer = await customers.FindForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The endpoint keeps the tenant's customer before its checkout.");
        var checkout = await checkouts.FindForUpdateAsync(cancellationToken);
        if (checkout is not null && checkout.Record(command.Key, command.SessionId, command.Url.ToString()))
        {
            await checkouts.SaveChangesAsync(cancellationToken);
            return (true, []);
        }

        return (false, [new ExpireOtherCheckoutsCommand(customer.CustomerId, checkout?.Key)]);
    }
}
