using Audit.Application.AuditLogs;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Audit.Application.Tenants.PurgeTenantData;

// The tenant purge asks Audit to delete the tenant's log, in the tenant's transaction (section 6, "Tenant purge"). The platform's log
// keeps its entries about the tenant (K3). A log already empty stays empty, so the step may run twice (S7).
public static class PurgeTenantDataHandler
{
    /// <summary>The name Audit takes part in the purge by, which its replies carry.</summary>
    public const string Participant = "Audit";

    public static async Task<OutgoingMessages> HandleAsync(PurgeTenantDataCommand purge, IAuditLog log, CancellationToken cancellationToken)
    {
        await log.PurgeTenantEntriesAsync(cancellationToken);
        return [new PurgeTenantDataReply(purge.TenantId, Participant)];
    }
}
