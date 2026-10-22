using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Members;

namespace Audit.Application.Members.RecordMemberRoleChanged;

// The audit entry of a member's new role, in the tenant's own log, under that tenant: the member who changed it acted, and the member
// whose role changed is the target, by id (K3). The details hold the old and the new role.
public static class RecordMemberRoleChangedHandler
{
    public static Task HandleAsync(MemberRoleChangedEvent changed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(
                changed.EventId,
                changed.OccurredAt,
                AuditActor.User(changed.ChangedBy),
                AuditOperations.MemberRoleChanged,
                AuditTarget.User(changed.UserId),
                JsonSerializer.Serialize(new MemberRoleChangedDetails(
                        changed.OldRole,
                        changed.NewRole,
                        changed.OldRoleId,
                        changed.OldRoleBuiltIn,
                        changed.NewRoleId,
                        changed.NewRoleBuiltIn), JsonSerializerOptions.Web)),
            cancellationToken);
}
