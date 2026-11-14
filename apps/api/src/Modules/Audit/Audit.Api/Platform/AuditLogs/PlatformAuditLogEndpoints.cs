using Audit.Api.Tenant.AuditLogs;
using Audit.Application.AuditLogs;
using Audit.Application.AuditLogs.GetAuditEntryNames;
using Audit.Application.AuditLogs.GetAuditInvitationEmails;
using Audit.Application.AuditLogs.ListPlatformAuditEntries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SharedKernel;
using Wolverine;

namespace Audit.Api.Platform.AuditLogs;

/// <summary>The platform's audit log: what system admins did, above tenants.</summary>
public static class PlatformAuditLogEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapGet("/audit-log", ListPlatformAuditEntries).RequireAuthorization(AuditLogPermissions.PlatformAuditRead);
        platform.MapGet("/audit-log/retention", GetPlatformAuditRetention).RequireAuthorization(AuditLogPermissions.PlatformAuditRead);
    }

    // K15: the deployment setting that the daily job reads, validated at the start, not a plan's value. No table holds it, so no message
    // and no transaction is needed to read it.
    private static Result<PlatformAuditRetentionResponse> GetPlatformAuditRetention(IOptions<AuditRetentionOptions> retention) =>
        new PlatformAuditRetentionResponse(retention.Value.Days);

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

        var entries = await bus.InvokeAsync<Result<CursorPage<AuditEntryView>>>(new ListPlatformAuditEntriesQuery(page.Value, tenantId), cancellationToken);
        if (!entries.IsSuccess)
        {
            return entries.Error;
        }

        var named = await bus.InvokeAsync<Result<CursorPage<AuditEntryView>>>(new GetAuditEntryNamesQuery(entries.Value), cancellationToken);
        if (!named.IsSuccess)
        {
            return named.Error;
        }

        // The invitations are tenants' rows, which this transaction does not see (R11): one query in each tenant on the page that has an
        // invitation entry, and none for a page without one (W3).
        var emails = new Dictionary<Guid, string>();
        foreach (var (tenant, invitations) in AuditInvitationEmails.ByTenant(named.Value.Items))
        {
            var found = await bus.InvokeForTenantAsync<IReadOnlyDictionary<Guid, string>>(
                tenant.ToString(), new GetAuditInvitationEmailsQuery(invitations), cancellationToken);
            foreach (var (invitation, email) in found)
            {
                emails[invitation] = email;
            }
        }

        return AuditInvitationEmails.With(named.Value, emails).Map(AuditLogEndpoints.ToResponse);
    }
}
