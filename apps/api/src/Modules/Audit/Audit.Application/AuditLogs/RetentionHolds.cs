using ControlPlane.Contracts.Plans;

namespace Audit.Application.AuditLogs;

// K14: a plan change and an override that shorten the tenant's Activity log period start the hold at the event's moment, in the transaction
// that wrote the event's entry, after it (K6o). A copy of the event finds its entry written, and starts nothing more (R18).
internal static class RetentionHolds
{
    public static async Task StartAsync(
        IRetentionStore retention,
        bool recorded,
        AuditRetentionShortening? shortening,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (!recorded || shortening is null)
        {
            return;
        }

        var row = await retention.LockTenantAsync(cancellationToken);
        row.Shorten(shortening.ToDays, at, TimeSpan.FromDays(AuditRetention.Grace));
        await retention.SaveAsync(cancellationToken);
    }
}
