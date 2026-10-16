using ControlPlane.Contracts;
using SharedKernel;

namespace Audit.Application.AuditLogs;

/// <summary>A page of an audit log with each user, actor or target, named now by the catalog, in one call for the page (K3).</summary>
public sealed record GetAuditEntryNames(CursorPage<AuditEntrySummary> Page);

public static class GetAuditEntryNamesHandler
{
    public static Task<CursorPage<AuditEntrySummary>> HandleAsync(GetAuditEntryNames query, IUserNames userNames, CancellationToken cancellationToken) =>
        AuditEntrySummaries.NamedAsync(query.Page, userNames, cancellationToken);
}
