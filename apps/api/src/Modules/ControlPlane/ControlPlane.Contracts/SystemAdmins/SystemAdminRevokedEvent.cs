using SharedKernel;

namespace ControlPlane.Contracts.SystemAdmins;

/// <summary>A system admin took the grant away from another system admin. Published through the outbox, without a tenant.</summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the former system admin, who stays a user.</param>
/// <param name="RevokedBy">The user id of the system admin who revoked it.</param>
[MessageName("system-admin-revoked")]
public sealed record SystemAdminRevokedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, Guid RevokedBy);
