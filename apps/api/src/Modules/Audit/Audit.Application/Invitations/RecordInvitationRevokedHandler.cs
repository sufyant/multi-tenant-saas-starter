using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Invitations;

// The audit record of a revoked invitation, in the tenant's own log, under that tenant: the member who revoked it acted, and the
// invitation is the target, by its id.
public static class RecordInvitationRevokedHandler
{
    public static Task HandleAsync(InvitationRevoked revoked, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new AuditEntry(revoked.EventId, revoked.OccurredAt, AuditActor.User(revoked.RevokedBy), AuditOperations.InvitationRevoked, AuditTarget.Invitation(revoked.InvitationId)),
            cancellationToken);
}
