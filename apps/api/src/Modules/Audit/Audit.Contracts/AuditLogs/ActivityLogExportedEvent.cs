using SharedKernel;

namespace Audit.Contracts.AuditLogs;

/// <summary>
/// A member exported the tenant's Activity log to a file: the web's server writes it after this event's command. Published through the
/// outbox; Audit handles it in the tenant's transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="ExportedBy">The user id of the member who exported it, who acted.</param>
[MessageName("activity-log-exported")]
public sealed record ActivityLogExportedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid ExportedBy);
