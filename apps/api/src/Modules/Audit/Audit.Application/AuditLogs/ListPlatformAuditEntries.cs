using Audit.Application.Ports;
using SharedKernel;

namespace Audit.Application.AuditLogs;

/// <summary>
/// The platform's audit log, a page at a time, newest first (K5's cursor), or only its entries about the tenant, for the tenant's Activity
/// tab on the platform. It runs without a tenant and without a user (R11): it never reads the tenant's own log. No user is named yet:
/// <see cref="GetAuditEntryNamesQuery"/> names them.
/// </summary>
public sealed record ListPlatformAuditEntriesQuery(CursorRequest Page, Guid? TenantId = null);

public static class ListPlatformAuditEntriesHandler
{
    public static async Task<CursorPage<AuditEntryView>> HandleAsync(ListPlatformAuditEntriesQuery query, IAuditLog log, CancellationToken cancellationToken) =>
        AuditEntryViews.PageOf(await log.ReadPlatformLogAsync(query.Page, query.TenantId, cancellationToken), query.Page);
}
