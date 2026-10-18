using System.Text.Json;
using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Invitations;

// The audit record of an invitation, in the tenant's own log, under that tenant: the member who invited acted, and the invitation is the
// target, by its id. The details hold the role and the invitation it replaced. The event carries the invited address for Notifications;
// the record stores none (K3).
public static class RecordMemberInvitedHandler
{
    public static Task HandleAsync(MemberInvited invited, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new AuditEntry(
                invited.EventId,
                invited.OccurredAt,
                AuditActor.User(invited.InvitedBy),
                AuditOperations.MemberInvited,
                AuditTarget.Invitation(invited.InvitationId),
                JsonSerializer.Serialize(new Details(invited.Role, invited.ReplacedInvitationId), JsonSerializerOptions.Web)),
            cancellationToken);

    private sealed record Details(string Role, Guid? ReplacedInvitationId);
}
