using System.Text.Json;
using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Users;

// A system admin's deletion of a user takes the user out of each tenant. The audit record of each removal is in the tenant's own log, under
// that tenant: the platform acted, without an id, and the removed member is the target, by id (K3). The details hold the role the member
// had, as a member's removal does. The deletion's own record, user.deleted, is in the platform's log.
public static class RecordUserMembershipRemovedHandler
{
    public static Task HandleAsync(UserMembershipRemovedEvent removed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new AuditEntry(
                removed.EventId,
                removed.OccurredAt,
                AuditActor.Platform,
                AuditOperations.MemberRemoved,
                AuditTarget.User(removed.UserId),
                JsonSerializer.Serialize(new Details(removed.Role), JsonSerializerOptions.Web)),
            cancellationToken);

    private sealed record Details(string Role);
}
