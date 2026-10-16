using System.Text.Json;
using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Members;

// The audit record of a member's new role, in the tenant's own log, under that tenant: the member who changed it acted, and the member
// whose role changed is the target, by id (K3). The details hold the old and the new role.
public static class RecordMemberRoleChangedHandler
{
    private const string MemberRoleChanged = "member.role_changed";

    public static Task HandleAsync(MemberRoleChanged changed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new AuditEntry(
                changed.EventId,
                changed.OccurredAt,
                AuditActor.User(changed.ChangedBy),
                MemberRoleChanged,
                AuditTarget.User(changed.UserId),
                JsonSerializer.Serialize(new Details(changed.OldRole, changed.NewRole), JsonSerializerOptions.Web)),
            cancellationToken);

    private sealed record Details(string OldRole, string NewRole);
}
