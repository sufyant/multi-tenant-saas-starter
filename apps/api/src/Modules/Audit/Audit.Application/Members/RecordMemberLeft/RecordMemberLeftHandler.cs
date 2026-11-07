using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Members;

namespace Audit.Application.Members.RecordMemberLeft;

// The audit entry of a member who left, in the tenant's own log, under that tenant: the member acted, and is the target, by id (K3). The
// details hold the role the member had.
public static class RecordMemberLeftHandler
{
    public static Task HandleAsync(MemberLeftEvent left, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(
                left.EventId,
                left.OccurredAt,
                AuditActor.User(left.UserId),
                AuditOperations.MemberLeft,
                AuditTarget.User(left.UserId),
                JsonSerializer.Serialize(new MemberLeftDetails(left.Role, left.RoleId, left.RoleBuiltIn), JsonSerializerOptions.Web)),
            cancellationToken);
}
