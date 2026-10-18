using ControlPlane.Contracts.Users;
using SharedKernel;

namespace Audit.Application.AuditLogs.GetAuditEntryNames;

public static class GetAuditEntryNamesHandler
{
    public static async Task<Result<CursorPage<AuditEntryView>>> HandleAsync(GetAuditEntryNamesQuery query, IUserDirectory users, CancellationToken cancellationToken) =>
        await AuditEntryViews.NamedAsync(query.Page, users, cancellationToken);
}
