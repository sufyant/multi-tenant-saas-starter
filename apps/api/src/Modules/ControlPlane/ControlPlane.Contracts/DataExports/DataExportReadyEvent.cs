using SharedKernel;

namespace ControlPlane.Contracts.DataExports;

/// <summary>
/// The worker prepared the user's export, which they can download until it expires. Notifications tells the user in the personal bell and
/// by email, with a link to Account and security, never to the file. Published through the outbox, without a tenant.
/// </summary>
/// <param name="ExportId">The export, which the notification's id names, so a copy of the event tells the user once.</param>
[MessageName("data-export-ready")]
public sealed record DataExportReadyEvent(Guid ExportId, DateTimeOffset OccurredAt, Guid UserId, DateTimeOffset ExpiresAt);
