using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Invitations;

namespace Audit.Application.Invitations.RecordOwnerInvited;

// The audit entries of an Owner the platform invited into a tenant without one, written when ControlPlane announces it, under that
// tenant. The system admin who invited acted: the platform's log names them, and the tenant's own log says only that the platform acted
// (K3). The target is the tenant, with its name of that moment, so the platform's Activity tab of the tenant shows it. The details hold
// the invitation's id and the one it replaced, never the address.
public static class RecordOwnerInvitedHandler
{
    public static Task HandleAsync(OwnerInvitedEvent invited, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(invited.TenantId, invited.TenantName);
        var details = JsonSerializer.Serialize(new TenantOwnerInvitedDetails(invited.InvitationId, invited.ReplacedInvitationId), JsonSerializerOptions.Web);
        return log.RecordAsync(
            new PlatformAuditEntry(invited.EventId, invited.OccurredAt, AuditActor.User(invited.InvitedBy), AuditOperations.TenantOwnerInvited, tenant, details),
            new TenantAuditEntry(invited.EventId, invited.OccurredAt, AuditActor.Platform, AuditOperations.TenantOwnerInvited, tenant, details),
            cancellationToken);
    }
}
