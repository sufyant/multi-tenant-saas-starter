using SharedKernel;

namespace ControlPlane.Contracts.DataExports;

/// <summary>The user asked for a copy of their data (GDPR, Articles 15 and 20). Published through the outbox, without a tenant.</summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the user, who acted for themselves.</param>
/// <param name="ExportId">The export, which the worker prepares.</param>
[MessageName("data-export-requested")]
public sealed record DataExportRequestedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, Guid ExportId);
