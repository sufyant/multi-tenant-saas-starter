using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Invitations;

namespace Audit.Application.Invitations.RecordInvitationRevoked;

// The audit entry of a revoked invitation, in the tenant's own log, under that tenant: the member who revoked it acted, and the
// invitation is the target, by its id.
public static class RecordInvitationRevokedHandler
{
    public static Task HandleAsync(InvitationRevokedEvent revoked, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(revoked.EventId, revoked.OccurredAt, AuditActor.User(revoked.RevokedBy), AuditOperations.InvitationRevoked, AuditTarget.Invitation(revoked.InvitationId)),
            cancellationToken);
}
