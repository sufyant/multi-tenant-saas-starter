using System.Reflection;
using Audit.Contracts.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;

namespace Audit.Application.AuditLogs.RecordAuditEntry;

// The audit entry of a product module's change, in the tenant's own log, under that tenant: the member acted, and the tenant is the
// target (X8). Audit knows the tenant by its id only, so the target keeps no name. Only an operation of the product is accepted: a
// template's operation comes only from the template's own events (K3), so a product cannot write one.
public static class RecordAuditEntryHandler
{
    private static readonly HashSet<string> ProductOperations =
    [
        .. typeof(ProductAuditOperations).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!),
    ];

    public static Task HandleAsync(RecordAuditEntryCommand command, IAuditLog log, CancellationToken cancellationToken) =>
        ProductOperations.Contains(command.Operation)
            ? log.RecordAsync(
                null,
                new TenantAuditEntry(
                    command.EventId,
                    command.OccurredAt,
                    AuditActor.User(command.ActorId),
                    command.Operation,
                    AuditTarget.Tenant(command.TenantId, null),
                    null),
                cancellationToken)
            : throw new ArgumentException($"{command.Operation} is not an operation of ProductAuditOperations (X6).", nameof(command));
}
