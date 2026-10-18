using Audit.Application.Ports;
using SharedKernel;

namespace Audit.Application.AuditLogs;

/// <summary>
/// The active tenant's audit log, a page at a time, newest first (K5's cursor). The screen calls it "Activity". No user is
/// named yet: <see cref="GetAuditEntryNamesQuery"/> names them.
/// </summary>
public sealed record ListAuditEntriesQuery(CursorRequest Page);

public static class ListAuditEntriesHandler
{
    public static async Task<CursorPage<AuditEntryView>> HandleAsync(ListAuditEntriesQuery query, IAuditLog log, CancellationToken cancellationToken) =>
        AuditEntryViews.PageOf(await log.ListTenantEntriesAsync(query.Page, cancellationToken), query.Page);
}
