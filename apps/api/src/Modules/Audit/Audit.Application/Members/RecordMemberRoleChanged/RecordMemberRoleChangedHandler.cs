using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Members;

namespace Audit.Application.Members.RecordMemberRoleChanged;

// The audit entry of a member's new role, in the tenant's own log, under that tenant: the member who changed it acted, and the member
// whose role changed is the target, by id (K3). The details hold the old and the new role. When the platform made the member an Owner (M2),
// the platform's log names the system admin, and the tenant's log says only that the platform acted, with the same target and details.
public static class RecordMemberRoleChangedHandler
{
    public static Task HandleAsync(MemberRoleChangedEvent changed, IAuditLog log, CancellationToken cancellationToken)
    {
        var target = AuditTarget.User(changed.UserId);
        var details = JsonSerializer.Serialize(
            new MemberRoleChangedDetails(changed.OldRole, changed.NewRole, changed.OldRoleId, changed.OldRoleBuiltIn, changed.NewRoleId, changed.NewRoleBuiltIn),
            JsonSerializerOptions.Web);
        var platform = changed.ByPlatform == true
            ? new PlatformAuditEntry(changed.EventId, changed.OccurredAt, AuditActor.User(changed.ChangedBy), AuditOperations.MemberRoleChanged, target, details)
            : null;
        var actor = platform is null ? AuditActor.User(changed.ChangedBy) : AuditActor.Platform;
        return log.RecordAsync(
            platform,
            new TenantAuditEntry(changed.EventId, changed.OccurredAt, actor, AuditOperations.MemberRoleChanged, target, details),
            cancellationToken);
    }
}
