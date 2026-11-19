using Billing.Contracts.BillingEmails;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Billing.Application.BillingEmails.NameBillingEmailChange;

// The tenant's name of this moment, which the Activity log keeps (K3). A tenant that a purge deleted since records nothing. The event goes
// through the outbox, in the tenant of the envelope (W3).
public static class NameBillingEmailChangeHandler
{
    public static async Task<OutgoingMessages> HandleAsync(NameBillingEmailChangeCommand command, ITenantNames tenants, CancellationToken cancellationToken) =>
        await tenants.FindNameAsync(command.TenantId, cancellationToken) is { } tenantName
            ? [new BillingEmailChangedEvent(command.EventId, command.OccurredAt, command.TenantId, tenantName, command.ChangedBy, command.Removed)]
            : [];
}
