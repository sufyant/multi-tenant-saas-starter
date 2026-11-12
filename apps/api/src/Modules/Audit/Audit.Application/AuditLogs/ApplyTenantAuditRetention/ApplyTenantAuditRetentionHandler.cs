using Audit.Application.AuditLogs.DeleteExpiredEntries;
using ControlPlane.Contracts.Plans;
using Wolverine;

namespace Audit.Application.AuditLogs.ApplyTenantAuditRetention;

// K14: the tenant's effective period at this moment, through ControlPlane's contract, on ControlPlane's DbContext. A purging or a purged
// tenant gets nothing more, so a message late for the purge writes nothing. The next step writes Audit's row and deletes, on Audit's
// DbContext: one DbContext owns a handler's transaction (W1). It goes to the same tenant, which the envelope carries on.
public static class ApplyTenantAuditRetentionHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        ApplyTenantAuditRetentionCommand command,
        IAuditRetentionDirectory periods,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        await periods.FindAsync(command.TenantId, cancellationToken) is { } period
            ? [new DeleteExpiredEntriesCommand(command.TenantId, period.Days, time.GetUtcNow(), command.Readings)]
            : [];
}
