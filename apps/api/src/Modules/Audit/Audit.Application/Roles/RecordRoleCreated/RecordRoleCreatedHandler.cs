using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Roles;

namespace Audit.Application.Roles.RecordRoleCreated;

// The audit entry of a new custom role, in the tenant's own log, under that tenant: the member who created it acted, and the role is the
// target, with its name of that moment, because a role is not a person (K3). The details hold its permissions.
public static class RecordRoleCreatedHandler
{
    public static Task HandleAsync(RoleCreatedEvent created, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(
                created.EventId,
                created.OccurredAt,
                AuditActor.User(created.CreatedBy),
                AuditOperations.RoleCreated,
                AuditTarget.Role(created.RoleId, created.Name),
                JsonSerializer.Serialize(new RoleCreatedDetails(created.Permissions), JsonSerializerOptions.Web)),
            cancellationToken);
}
