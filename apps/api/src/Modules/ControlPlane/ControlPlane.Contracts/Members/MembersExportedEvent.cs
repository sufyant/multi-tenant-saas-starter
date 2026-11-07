using SharedKernel;

namespace ControlPlane.Contracts.Members;

/// <summary>
/// A member exported the tenant's members to a file: the web's server writes it after this event's command. Published through the outbox;
/// a subscriber handles it in the tenant's transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="ExportedBy">The user id of the member who exported them, who acted.</param>
/// <param name="TenantName">The tenant's name at that moment, for its audit entry.</param>
[MessageName("members-exported")]
public sealed record MembersExportedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid ExportedBy, string TenantName);
