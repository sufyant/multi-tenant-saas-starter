using ControlPlane.Contracts.Plans;
using SharedKernel;

namespace Audit.Application.AuditLogs.GetAuditRetention;

// K14: what the daily job would do at this moment, by the job's own rule on the same state, so the Banner never shows a date the job does
// not keep. The row follows the period and clears an ended hold in memory only: the read takes no lock and writes nothing. A shortening the
// job has not seen yet, a plan edit or an expiry, answers the hold that the job would start now; the job's run, up to a day later, starts
// it at its own moment, so the date shown is never later than the job's (the drift of at most a day that step 37c accepted). Row level security keeps the row and the
// entries to the tenant (R1).
public static class GetAuditRetentionHandler
{
    public static async Task<Result<AuditRetentionView>> HandleAsync(
        GetAuditRetentionQuery query,
        IRetentionStore retention,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var row = await retention.FindTenantAsync(cancellationToken);
        if (row.HoldAt(query.Days, now, TimeSpan.FromDays(AuditRetention.Grace)) is not { } hold
            || !await retention.HasEntriesOlderThanAsync(now.AddDays(-hold.Days), hold.HeldFrom, cancellationToken))
        {
            return new AuditRetentionView(query.Days, null);
        }

        return new AuditRetentionView(query.Days, new AuditRetentionShorteningView(hold.Days, hold.GoesAt));
    }
}
