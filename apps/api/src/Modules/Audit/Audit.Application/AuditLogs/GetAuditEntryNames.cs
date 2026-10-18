using ControlPlane.Contracts;
using SharedKernel;

namespace Audit.Application.AuditLogs;

/// <summary>A page of an audit log with each user, actor or target, named now by the catalog, in one call for the page (K3).</summary>
public sealed record GetAuditEntryNamesQuery(CursorPage<AuditEntryView> Page);

public static class GetAuditEntryNamesHandler
{
    public static Task<CursorPage<AuditEntryView>> HandleAsync(GetAuditEntryNamesQuery query, IUserNames userNames, CancellationToken cancellationToken) =>
        AuditEntryViews.NamedAsync(query.Page, userNames, cancellationToken);
}
