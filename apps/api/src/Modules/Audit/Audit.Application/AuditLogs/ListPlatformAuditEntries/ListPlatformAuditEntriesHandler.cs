using SharedKernel;

namespace Audit.Application.AuditLogs.ListPlatformAuditEntries;

public static class ListPlatformAuditEntriesHandler
{
    public static async Task<CursorPage<AuditEntryView>> HandleAsync(ListPlatformAuditEntriesQuery query, IAuditLog log, CancellationToken cancellationToken) =>
        AuditEntryViews.PageOf(await log.ListPlatformEntriesAsync(query.Page, query.TenantId, cancellationToken), query.Page);
}
