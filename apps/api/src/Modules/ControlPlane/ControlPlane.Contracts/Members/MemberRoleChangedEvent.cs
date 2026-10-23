using SharedKernel;

namespace ControlPlane.Contracts.Members;

/// <summary>
/// A member gave a member another role of the tenant, a member changed their own, or the platform made a member an Owner (M2, Owner
/// recovery). Published through the outbox only when the role changed; a subscriber handles it in the tenant's transaction, as the
/// tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the member whose role changed.</param>
/// <param name="ChangedBy">The user id of the member who changed it, who acted; the system admin's when the platform acted.</param>
/// <param name="OldRole">The name of the role the member had: <c>owner</c>, <c>admin</c>, <c>member</c> or a custom role's.</param>
/// <param name="NewRole">The name of the role the member has now.</param>
/// <param name="OldRoleId">The id of the role the member had. Null only in a message that a version before step r4b published.</param>
/// <param name="OldRoleBuiltIn">Whether the role the member had is built in. Null only in a message that a version before step r4b published.</param>
/// <param name="NewRoleId">The id of the role the member has now. Null only in a message that a version before step r4b published.</param>
/// <param name="NewRoleBuiltIn">Whether the role the member has now is built in. Null only in a message that a version before step r4b published.</param>
/// <param name="ByPlatform">True when a system admin made the member an Owner, so the tenant's log names the platform (K3). Null in a message
/// that a version before step r6 published, which only a member sent.</param>
[MessageName("member-role-changed")]
public sealed record MemberRoleChangedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid UserId,
    Guid ChangedBy,
    string OldRole,
    string NewRole,
    Guid? OldRoleId,
    bool? OldRoleBuiltIn,
    Guid? NewRoleId,
    bool? NewRoleBuiltIn,
    bool? ByPlatform = null);
