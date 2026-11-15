using Billing.Application.Checkouts.ExpireCheckoutPages;
using Billing.Application.Customers;
using Wolverine;

namespace Billing.Application.Checkouts.RecordCheckoutSession;

// The tenant's customer is locked first (K6a, B7). A page of a replaced key may have been made after the replacement expired the customer's
// open pages of that key, so it is expired here, by its key.
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

        return (false, [new ExpireCheckoutPagesCommand(customer.CustomerId, command.Key)]);
    }
}
