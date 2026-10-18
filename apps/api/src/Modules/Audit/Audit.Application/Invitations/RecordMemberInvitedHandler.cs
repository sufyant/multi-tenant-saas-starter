using System.Text.Json;
using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Invitations;

// The audit entry of an invitation, in the tenant's own log, under that tenant: the member who invited acted, and the invitation is the
// target, by its id. The details hold the role and the invitation it replaced. The event carries the invited address for Notifications;
// the entry stores none (K3).
public static class RecordMemberInvitedHandler
{
    public static Task HandleAsync(MemberInvitedEvent invited, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(
                invited.EventId,
                invited.OccurredAt,
                AuditActor.User(invited.InvitedBy),
                AuditOperations.MemberInvited,
                AuditTarget.Invitation(invited.InvitationId),
                JsonSerializer.Serialize(new Details(invited.Role, invited.ReplacedInvitationId), JsonSerializerOptions.Web)),
            cancellationToken);

    private sealed record Details(string Role, Guid? ReplacedInvitationId);
}
