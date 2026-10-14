using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Tenants;

// The audit record of a tenant's suspension, written when ControlPlane announces it, under that tenant. The system admin who suspended
// the tenant acted.
public static class RecordTenantSuspendedHandler
{
    private const string TenantSuspended = "tenant.suspended";

    // An event may arrive twice; its id is the entry's id, so an entry already stored is not stored again.
    public static async Task HandleAsync(TenantSuspended suspended, IAuditLog log, CancellationToken cancellationToken)
    {
        if (await log.ContainsAsync(suspended.EventId, cancellationToken))
        {
            return;
        }

        await log.AddAsync(
            new AuditEntry(suspended.EventId, suspended.OccurredAt, suspended.SuspendedBy.ToString(), AuditEntryKind.Command, TenantSuspended, null),
            cancellationToken);
    }
}
