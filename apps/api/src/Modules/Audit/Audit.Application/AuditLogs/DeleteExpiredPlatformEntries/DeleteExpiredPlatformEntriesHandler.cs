using Microsoft.Extensions.Options;
using Wolverine;

namespace Audit.Application.AuditLogs.DeleteExpiredPlatformEntries;

// K15: one batch in each transaction, which declares nobody (R11), as the notifications' retention (U8). A full batch sends the next one. A
// lower setting applies here, with no hold.
public static class DeleteExpiredPlatformEntriesHandler
{
    /// <summary>How many entries one transaction deletes at most.</summary>
    public const int BatchSize = 1000;

    public static async Task<OutgoingMessages> HandleAsync(
        DeleteExpiredPlatformEntriesCommand command,
        IRetentionStore retention,
        IOptions<AuditRetentionOptions> options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var deleted = await retention.DeleteExpiredPlatformEntriesAsync(time.GetUtcNow().AddDays(-options.Value.Days), BatchSize, cancellationToken);
        return deleted == BatchSize ? [command] : [];
    }
}
