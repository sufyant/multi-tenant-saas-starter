using SharedKernel;

namespace ControlPlane.Contracts.DataExports;

/// <summary>The user took a download link of their ready export. Published through the outbox, without a tenant.</summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the user, who acted for themselves.</param>
[MessageName("data-export-downloaded")]
public sealed record DataExportDownloadedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, Guid ExportId);
