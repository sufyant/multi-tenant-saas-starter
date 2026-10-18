using System.Text.Json;
using Audit.Application.Ports;
using Audit.Domain;
using ControlPlane.Contracts;

namespace Audit.Application.Members;

// The audit entry of a removed member, in the tenant's own log, under that tenant: the member who removed them acted, and the removed
// member is the target, by id (K3). The details hold the role the member had.
public static class RecordMemberRemovedHandler
{
    public static Task HandleAsync(MemberRemovedEvent removed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(
                removed.EventId,
                removed.OccurredAt,
                AuditActor.User(removed.RemovedBy),
                AuditOperations.MemberRemoved,
                AuditTarget.User(removed.UserId),
                JsonSerializer.Serialize(new Details(removed.Role), JsonSerializerOptions.Web)),
            cancellationToken);

    private sealed record Details(string Role);
}
