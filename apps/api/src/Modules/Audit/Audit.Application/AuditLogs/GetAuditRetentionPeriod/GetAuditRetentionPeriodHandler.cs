using ControlPlane.Contracts.Plans;
using SharedKernel;

namespace Audit.Application.AuditLogs.GetAuditRetentionPeriod;

// K14: the period through the contract that the daily job reads, on ControlPlane's DbContext, so the screen and the job read one value.
// The Audit row is read by a second query, on Audit's DbContext: one DbContext owns a handler's transaction (W1). The door refuses a
// purging or a purged tenant's members, so the contract finds the tenant; a purge that starts between the door and this read leaves no
// period, and its log goes with the purge (R14).
public static class GetAuditRetentionPeriodHandler
{
    public static async Task<Result<AuditRetentionPeriodView>> HandleAsync(
        GetAuditRetentionPeriodQuery query,
        IAuditRetentionDirectory periods,
        CancellationToken cancellationToken) =>
        new AuditRetentionPeriodView((await periods.FindAsync(query.TenantId, cancellationToken))?.Days);
}
