using Billing.Application.BillingEmails.KeepCustomerEmail;
using ControlPlane.Contracts.Users;
using Wolverine;

namespace Billing.Application.BillingEmails.NameCustomerEmail;

// A member who paid first and was deleted since has no address: the provider keeps the one it has.
public static class NameCustomerEmailHandler
{
    public static async Task<OutgoingMessages> HandleAsync(NameCustomerEmailCommand command, IUserDirectory users, CancellationToken cancellationToken) =>
        (command.Address ?? (await users.FindContactsAsync([command.PaidBy], cancellationToken)).SingleOrDefault()?.Email) is { } email
            ? [new KeepCustomerEmailCommand(command.CustomerId, email)]
            : [];
}
