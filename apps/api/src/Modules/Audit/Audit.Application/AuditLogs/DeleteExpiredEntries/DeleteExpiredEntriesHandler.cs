using Audit.Application.AuditLogs.ApplyTenantAuditRetention;
using ControlPlane.Contracts.Plans;
using Wolverine;

namespace Audit.Application.AuditLogs.DeleteExpiredEntries;

// K14: under the row's lock (K6o), the row follows the period only while the tenant still has an entry. A purge takes the same lock and
// deletes every entry and the row, so a step after the purge writes nothing, and one before it is undone by it. A shorter period starts
// the hold at this moment, which is how a plan edit and an expired override hold, as they have no event for the tenant. Then one batch
// goes, in this transaction. A full batch starts the tenant's next run, which reads the period again. A message older than a minute
// writes and deletes nothing: the period may have got longer since the first step, so it reads the period again, at most three times in one
// run, and then the tenant waits for the next run; a day late keeps entries longer, never shorter (the maintainer's decision of 2026-11-12).
public static class DeleteExpiredEntriesHandler
{
    /// <summary>How many entries one transaction deletes at most.</summary>
    public const int BatchSize = 1000;

    /// <summary>How many times one run reads the period again for an old message.</summary>
    public const int MaxReadings = 3;

    /// <summary>How old the first step's period may be when this step uses it.</summary>
    public static readonly TimeSpan Freshness = TimeSpan.FromMinutes(1);

    public static async Task<OutgoingMessages> HandleAsync(
        DeleteExpiredEntriesCommand command,
        IRetentionStore retention,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (now - command.ReadAt > Freshness)
        {
            return command.Readings < MaxReadings ? [new ApplyTenantAuditRetentionCommand(command.TenantId, command.Readings + 1)] : [];
        }

        var row = await retention.LockTenantAsync(cancellationToken);
        if (!await retention.HasEntriesAsync(cancellationToken))
        {
            await retention.RemoveTenantAsync(row, cancellationToken);
            return [];
        }

        row.Follow(command.Days, now, TimeSpan.FromDays(AuditRetention.Grace));
        var before = row.ExpiresBefore(now);
        await retention.SaveAsync(cancellationToken);
        if (before is not { } moment)
        {
            return [];
        }

        var deleted = await retention.DeleteExpiredEntriesAsync(moment, BatchSize, cancellationToken);
        return deleted == BatchSize ? [new ApplyTenantAuditRetentionCommand(command.TenantId, command.Readings)] : [];
    }
}
