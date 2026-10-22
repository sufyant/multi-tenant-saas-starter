using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Roles;

namespace Audit.Application.Roles.RecordRoleUpdated;

// The audit entry of a changed custom role, in the tenant's own log, under that tenant: the member who changed it acted, and the role is
// the target, with its new name. The details hold the old and the new name and the permissions added and removed.
public static class RecordRoleUpdatedHandler
{
    public static Task HandleAsync(RoleUpdatedEvent updated, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(
                updated.EventId,
                updated.OccurredAt,
                AuditActor.User(updated.UpdatedBy),
                AuditOperations.RoleUpdated,
                AuditTarget.Role(updated.RoleId, updated.NewName),
                JsonSerializer.Serialize(
                    new RoleUpdatedDetails(updated.OldName, updated.NewName, updated.PermissionsAdded, updated.PermissionsRemoved),
                    JsonSerializerOptions.Web)),
            cancellationToken);
}
