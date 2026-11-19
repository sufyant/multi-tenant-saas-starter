using Billing.Contracts.Subscriptions;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Billing.Application.Subscriptions.NamePaymentFailure;

// The tenant's name of this moment, which the Activity log keeps (K3). A tenant that a purge deleted since records nothing. The event goes
// through the outbox, in the tenant of the envelope (W3).
public static class NamePaymentFailureHandler
{
    public static async Task<OutgoingMessages> HandleAsync(NamePaymentFailureCommand command, ITenantNames tenants, CancellationToken cancellationToken) =>
        await tenants.FindNameAsync(command.TenantId, cancellationToken) is { } tenantName
            ? [new PaymentFailedEvent(
                command.EventId, command.OccurredAt, command.TenantId, tenantName, command.AmountDue, command.Currency, command.RetriesUntil,
                command.FailedAt, command.NextAttemptAt, command.CardBrand, command.CardLast4, command.TenantClosed)]
            : [];
}
