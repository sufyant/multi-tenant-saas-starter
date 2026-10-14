using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Tenants;

// The audit record of a tenant's reactivation, written when ControlPlane announces it, under that tenant. The system admin who
// reactivated the tenant acted.
public static class RecordTenantReactivatedHandler
{
    private const string TenantReactivated = "tenant.reactivated";

    // An event may arrive twice; its id is the entry's id, so an entry already stored is not stored again.
    public static async Task HandleAsync(TenantReactivated reactivated, IAuditLog log, CancellationToken cancellationToken)
    {
        if (await log.ContainsAsync(reactivated.EventId, cancellationToken))
        {
            return;
        }

        await log.AddAsync(
            new AuditEntry(reactivated.EventId, reactivated.OccurredAt, reactivated.ReactivatedBy.ToString(), AuditEntryKind.Command, TenantReactivated, null),
            cancellationToken);
    }
}
