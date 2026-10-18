using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>
/// A system admin's deletion of a user took the user out of a tenant. Published through the outbox, in that tenant; Audit records it in
/// the tenant's log, as the platform's action (K3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The catalog user id of the user who left.</param>
/// <param name="Role">The built-in role the user had: <c>owner</c>, <c>admin</c> or <c>member</c>.</param>
[MessageName("user-membership-removed")]
public sealed record UserMembershipRemovedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid UserId, string Role);
