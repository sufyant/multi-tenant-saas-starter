using ControlPlane.Contracts.Users;
using SharedKernel;

namespace Audit.Application.AuditLogs.GetAuditEntryNames;

public static class GetAuditEntryNamesHandler
{
    public static Task<CursorPage<AuditEntryView>> HandleAsync(GetAuditEntryNamesQuery query, IUserDirectory users, CancellationToken cancellationToken) =>
        AuditEntryViews.NamedAsync(query.Page, users, cancellationToken);
}
