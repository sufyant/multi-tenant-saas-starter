using System.Text.Json;
using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Members;

// The audit record of a removed member, in the tenant's own log, under that tenant: the member who removed them acted, and the removed
// member is the target, by id (K3). The details hold the role the member had. A system admin's deletion of a user takes the user out of
// each tenant too; the tenant's log shows the platform as the actor, without an id. The deletion's own record, user.deleted, is in the
// platform's log.
public static class RecordMemberRemovedHandler
{
    private const string MemberRemoved = "member.removed";

    public static Task HandleAsync(MemberRemoved removed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new AuditEntry(
                removed.EventId,
                removed.OccurredAt,
                AuditActor.User(removed.RemovedBy),
                MemberRemoved,
                AuditTarget.User(removed.UserId),
                JsonSerializer.Serialize(new Details(RoleNames.Of(removed.Role)), JsonSerializerOptions.Web)),
            cancellationToken);

    public static Task HandleAsync(UserMembershipRemoved removed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new AuditEntry(
                removed.EventId,
                removed.OccurredAt,
                AuditActor.Platform,
                MemberRemoved,
                AuditTarget.User(removed.UserId),
                JsonSerializer.Serialize(new Details(RoleNames.Of(removed.Role)), JsonSerializerOptions.Web)),
            cancellationToken);

    private sealed record Details(string Role);
}
