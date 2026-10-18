using SharedKernel;

namespace Audit.Application.AuditLogs.ListAuditEntries;

public static class ListAuditEntriesHandler
{
    public static async Task<CursorPage<AuditEntryView>> HandleAsync(ListAuditEntriesQuery query, IAuditLog log, CancellationToken cancellationToken) =>
        AuditEntryViews.PageOf(await log.ListTenantEntriesAsync(query.Page, cancellationToken), query.Page);
}
