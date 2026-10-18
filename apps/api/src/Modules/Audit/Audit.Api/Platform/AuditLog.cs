using Audit.Api.Tenant;
using Audit.Application.AuditLogs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Audit.Api.Platform;

/// <summary>The platform's audit log: what system admins did, above tenants.</summary>
public static class AuditLog
{
    public static void Map(RouteGroupBuilder platform) =>
        platform.MapGet("/audit-log", ListPlatformAuditEntries).RequireAuthorization(AuditLogPermissions.PlatformAuditRead);

    // The log is above tenants, so the query runs without a tenant, and the platform door declares no user: the only transaction that
    // reads the platform's log (R11). K5's exception: a cursor, newest first, and no total. The tenant filter keeps the entries about that
    // tenant; a value that is no id is a request the endpoint cannot read (400 request.invalid). A cursor holds a place in the whole log,
    // so the next page sends the same filter.
    private static async Task<Result<CursorPage<AuditEntryResponse>>> ListPlatformAuditEntries(
        string? cursor,
        int? limit,
        Guid? tenantId,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var page = CursorRequest.Create(cursor, limit);
        if (!page.IsSuccess)
        {
            return page.Error;
        }

        var entries = await bus.InvokeAsync<CursorPage<AuditEntrySummary>>(new ListPlatformAuditEntriesQuery(page.Value, tenantId), cancellationToken);
        var named = await bus.InvokeAsync<CursorPage<AuditEntrySummary>>(new GetAuditEntryNamesQuery(entries), cancellationToken);
        return AuditEntryResponses.Of(named);
    }
}
