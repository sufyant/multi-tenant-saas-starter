using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Members;

// The audit record of a user who accepted an invitation, in the tenant's own log, under that tenant. The new member acted, and is the
// target too.
public static class RecordMemberJoinedHandler
{
    public static Task HandleAsync(MemberJoinedEvent joined, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new AuditEntry(joined.EventId, joined.OccurredAt, AuditActor.User(joined.UserId), AuditOperations.MemberJoined, AuditTarget.User(joined.UserId)),
            cancellationToken);
}
