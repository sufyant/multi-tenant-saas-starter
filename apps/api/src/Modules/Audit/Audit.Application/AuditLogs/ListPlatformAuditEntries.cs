using Audit.Application.Ports;
using ControlPlane.Contracts;
using SharedKernel;

namespace Audit.Application.AuditLogs;

/// <summary>
/// The platform's audit log, a page at a time, newest first (K5's cursor). It runs without a tenant and without a user (R11).
/// </summary>
public sealed record ListPlatformAuditEntries(CursorRequest Page);

public static class ListPlatformAuditEntriesHandler
{
    public static async Task<CursorPage<AuditEntrySummary>> HandleAsync(
        ListPlatformAuditEntries query,
        IAuditLog log,
        IUserNames userNames,
        CancellationToken cancellationToken) =>
        await AuditEntrySummaries.PageOfAsync(await log.ReadPlatformLogAsync(query.Page, cancellationToken), query.Page, userNames, cancellationToken);
}
