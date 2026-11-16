using Billing.Contracts.PriceChanges;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Billing.Application.PriceChanges.NamePriceChange;

// The tenant's name of this moment, which the Activity log keeps (K3). A tenant that a purge deleted since is told nothing. The event goes
// through the outbox, in the tenant of the envelope (W3).
public static class NamePriceChangeHandler
{
    public static async Task<OutgoingMessages> HandleAsync(NamePriceChangeCommand command, ITenantNames tenants, CancellationToken cancellationToken) =>
        await tenants.FindNameAsync(command.TenantId, cancellationToken) is { } tenantName
            ?
            [
                new PriceChangeAnnouncedEvent(
                    command.EventId, command.OccurredAt, command.TenantId, tenantName, command.PlanId, command.PlanName, command.Currency, command.Yearly,
                    command.OldPrices, command.NewPrices, command.MovesAt),
            ]
            : [];
}
