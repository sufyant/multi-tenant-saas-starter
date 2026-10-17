using Audit.Api.Tenant;
using Audit.Application.AuditLogs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Audit.Api.Platform;

/// <summary>The platform's audit log: what system admins and the identity provider did, above tenants.</summary>
public static class AuditLog
{
    // The name is stated here as in Tenant/AuditLog.cs, and checked against the catalogue in the same way.
    private const string PlatformAuditRead = "platform.audit.read";

    public static void Map(RouteGroupBuilder platform) =>
        platform.MapGet("/audit-log", ListPlatformAuditEntries).RequireAuthorization(PlatformAuditRead);

    // The log is above tenants, so the query runs without a tenant, and the platform door declares no user: the only transaction that
    // reads the platform's log (R11). K5's exception: a cursor, newest first, and no total.
    private static async Task<Result<CursorPage<AuditEntryResponse>>> ListPlatformAuditEntries(
        string? cursor,
        int? limit,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var page = CursorRequest.Create(cursor, limit);
        if (!page.IsSuccess)
        {
            return page.Error;
        }

        var entries = await bus.InvokeAsync<CursorPage<AuditEntrySummary>>(new ListPlatformAuditEntries(page.Value), cancellationToken);
        var named = await bus.InvokeAsync<CursorPage<AuditEntrySummary>>(new GetAuditEntryNames(entries), cancellationToken);
        return AuditEntryResponses.Of(named);
    }
}
