using System.Text.Json;
using Audit.Api.Statuses;
using Audit.Application.AuditLogs;
using Audit.Application.AuditLogs.GetAuditEntryNames;
using Audit.Application.AuditLogs.ListAuditEntries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The audit log of the tenant in the route, which its Owners and Admins read. The screen calls it "Activity".</summary>
public static class AuditLogEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet("/audit-log", ListAuditEntries).RequireAuthorization(AuditLogPermissions.TenantAuditRead);

    internal static AuditEntryResponse ToResponse(AuditEntryView entry) => new(
        entry.Id,
        entry.OccurredAt,
        entry.Operation,
        entry.Details is { } details ? JsonElement.Parse(details) : null,
        new AuditActorResponse(ResponseStatus.OfActor(entry.Actor.Kind), entry.Actor.UserId, entry.Actor.DisplayName, entry.Actor.Deleted),
        new AuditTargetResponse(ResponseStatus.OfTarget(entry.Target.Kind), entry.Target.Id, entry.Target.Name, entry.Target.Deleted));

    // The tenant comes from the route: the host has declared it, and row level security keeps the log to it. K5's exception: a cursor,
    // newest first, and no total.
    private static async Task<Result<CursorPage<AuditEntryResponse>>> ListAuditEntries(
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

        var entries = await bus.InvokeAsync<Result<CursorPage<AuditEntryView>>>(new ListAuditEntriesQuery(page.Value), cancellationToken);
        if (!entries.IsSuccess)
        {
            return entries.Error;
        }

        var named = await bus.InvokeAsync<Result<CursorPage<AuditEntryView>>>(new GetAuditEntryNamesQuery(entries.Value), cancellationToken);
        return named.Map(found => found.Map(ToResponse));
    }
}
