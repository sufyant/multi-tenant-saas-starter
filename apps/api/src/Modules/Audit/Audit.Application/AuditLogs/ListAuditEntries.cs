using Audit.Application.Ports;
using ControlPlane.Contracts;
using SharedKernel;

namespace Audit.Application.AuditLogs;

/// <summary>
/// The active tenant's audit log, a page at a time, newest first (K5's cursor). The screen calls it "Activity".
/// </summary>
public sealed record ListAuditEntries(CursorRequest Page);

public static class ListAuditEntriesHandler
{
    public static async Task<CursorPage<AuditEntrySummary>> HandleAsync(
        ListAuditEntries query,
        IAuditLog log,
        IUserNames userNames,
        CancellationToken cancellationToken) =>
        await AuditEntrySummaries.PageOfAsync(await log.ReadTenantLogAsync(query.Page, cancellationToken), query.Page, userNames, cancellationToken);
}
