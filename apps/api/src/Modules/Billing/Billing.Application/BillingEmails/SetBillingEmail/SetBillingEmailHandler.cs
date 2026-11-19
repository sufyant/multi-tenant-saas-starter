using Billing.Application.BillingEmails.NameBillingEmailChange;
using Billing.Application.BillingEmails.NameCustomerEmail;
using Billing.Application.Customers;
using Billing.Domain.BillingEmails;
using SharedKernel;
using Wolverine;

namespace Billing.Application.BillingEmails.SetBillingEmail;

// B14: the tenant's customer, the root of its billing, is locked first (B7), so two changes run one after the other. A change of the
// address asks for the provider's customer email, through the outbox in the same transaction: the address, or, once it is removed, the
// address of the member who opened the last checkout or changed the plan last. The tenant's Activity log records the change by the member, without the address (12.12). A tenant that has no customer yet gets the address at its first checkout. The same address changes
// nothing (I1).
public static class SetBillingEmailHandler
{
    public static async Task<(StateChange, OutgoingMessages)> HandleAsync(
        SetBillingEmailCommand command,
        ICustomerStore customers,
        IBillingEmailRepository emails,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var customer = await customers.FindForUpdateAsync(cancellationToken);
        var email = await emails.FindForUpdateAsync(cancellationToken);
        var change = (email, command.Address) switch
        {
            (null, null) => StateChange.Unchanged,
            (null, { } address) => Add(emails, new BillingEmail(address, command.SetBy, time.GetUtcNow())),
            ({ } kept, null) => Remove(emails, kept),
            ({ } kept, { } address) => kept.Change(address, command.SetBy, time.GetUtcNow()),
        };
        if (change == StateChange.Unchanged)
        {
            return (change, []);
        }

        await emails.SaveChangesAsync(cancellationToken);
        var now = time.GetUtcNow();
        OutgoingMessages messages = [new NameBillingEmailChangeCommand(Guid.CreateVersion7(now), now, command.TenantId, command.SetBy, command.Address is null)];
        if (customer is not null)
        {
            messages.Add(new NameCustomerEmailCommand(customer.CustomerId, command.Address, customer.CheckoutBy));
        }

        return (change, messages);
    }

    private static StateChange Add(IBillingEmailRepository emails, BillingEmail email)
    {
        emails.Add(email);
        return StateChange.Changed;
    }

    private static StateChange Remove(IBillingEmailRepository emails, BillingEmail email)
    {
        emails.Remove(email);
        return StateChange.Changed;
    }
}
