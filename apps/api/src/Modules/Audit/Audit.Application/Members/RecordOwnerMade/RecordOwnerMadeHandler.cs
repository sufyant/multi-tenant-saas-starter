using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Members;

namespace Audit.Application.Members.RecordOwnerMade;

// The audit entries of an Owner the platform made, written when ControlPlane announces it, under that tenant (K3). The platform's log
// names the system admin, and its target is the tenant, with its name of that moment, so the platform's Activity tab of the tenant shows
// it; the details hold the member's id and the role they had. The tenant's own log says only that the platform acted, and reads as a
// member's role change: the member is the target, and the details hold the old and the new role.
public static class RecordOwnerMadeHandler
{
    public static Task HandleAsync(OwnerMadeEvent made, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(
                made.EventId,
                made.OccurredAt,
                AuditActor.User(made.MadeBy),
                AuditOperations.TenantOwnerMade,
                AuditTarget.Tenant(made.TenantId, made.TenantName),
                JsonSerializer.Serialize(new TenantOwnerMadeDetails(made.UserId, made.OldRole, made.OldRoleId, made.OldRoleBuiltIn), JsonSerializerOptions.Web)),
            new TenantAuditEntry(
                made.EventId,
                made.OccurredAt,
                AuditActor.Platform,
                AuditOperations.MemberRoleChanged,
                AuditTarget.User(made.UserId),
                JsonSerializer.Serialize(
                    new MemberRoleChangedDetails(made.OldRole, made.NewRole, made.OldRoleId, made.OldRoleBuiltIn, made.NewRoleId, NewRoleBuiltIn: true),
                    JsonSerializerOptions.Web)),
            cancellationToken);
}
