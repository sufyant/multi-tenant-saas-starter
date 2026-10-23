using SharedKernel;

namespace ControlPlane.Contracts.SystemAdmins;

/// <summary>A user became a system admin (section 6, "Root system admins"). Published through the outbox, without a tenant.</summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the new system admin.</param>
/// <param name="GrantedBy">The user id of the system admin who granted it, or null when the system door granted a root system admin.</param>
[MessageName("system-admin-granted")]
public sealed record SystemAdminGrantedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, Guid? GrantedBy);
