using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Members;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Users;

namespace Audit.Application.Users.RecordUserMembershipRemoved;

// A system admin's deletion of a user takes the user out of each tenant. The audit entry of each removal is in the tenant's own log, under
// that tenant: the platform acted, without an id, and the removed member is the target, by id (K3). The details hold the role the member
// had, as a member's removal does. The deletion's own entry, user.deleted, is in the platform's log.
public static class RecordUserMembershipRemovedHandler
{
    public static Task HandleAsync(UserMembershipRemovedEvent removed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(
                removed.EventId,
                removed.OccurredAt,
                AuditActor.Platform,
                AuditOperations.MemberRemoved,
                AuditTarget.User(removed.UserId),
                JsonSerializer.Serialize(new MemberRemovedDetails(removed.Role, removed.RoleId, removed.RoleBuiltIn), JsonSerializerOptions.Web)),
            cancellationToken);
}
