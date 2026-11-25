using SharedKernel;

namespace Audit.Contracts.AuditLogs;

/// <summary>
/// A product module's audit entry: the module sends it through the outbox, in the transaction of its own change, and Audit writes the
/// entry into the tenant's own log (X8). Audit references no product contracts, so the product names its event's operation here. The
/// operation is a constant of <c>ProductAuditOperations</c>; the tenant is the target, and the entry keeps no details in step 41 (X6).
/// </summary>
/// <param name="EventId">Chosen by the sender, so a command that arrives twice writes one entry.</param>
/// <param name="TenantId">The tenant of the change, which the message's envelope also carries.</param>
/// <param name="ActorId">The user id of the member who acted.</param>
[MessageName("record-audit-entry")]
public sealed record RecordAuditEntryCommand(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid ActorId, string Operation);
