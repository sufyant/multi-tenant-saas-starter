using SharedKernel;

namespace ControlPlane.Contracts.Members;

/// <summary>
/// A member gave a member another role of the tenant, or a member changed their own. Published through the outbox only when the
/// role changed; a subscriber handles it in the tenant's transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the member whose role changed.</param>
/// <param name="ChangedBy">The user id of the member who changed it, who acted.</param>
/// <param name="OldRole">The name of the role the member had: <c>owner</c>, <c>admin</c>, <c>member</c> or a custom role's.</param>
/// <param name="NewRole">The name of the role the member has now.</param>
/// <param name="OldRoleId">The id of the role the member had.</param>
/// <param name="OldRoleBuiltIn">Whether the role the member had is built in.</param>
/// <param name="NewRoleId">The id of the role the member has now.</param>
/// <param name="NewRoleBuiltIn">Whether the role the member has now is built in.</param>
/// <param name="TenantName">The tenant's name at that moment, for a notification. None in an event of the version before step 33.</param>
[MessageName("member-role-changed")]
public sealed record MemberRoleChangedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid UserId,
    Guid ChangedBy,
    string OldRole,
    string NewRole,
    Guid OldRoleId,
    bool OldRoleBuiltIn,
    Guid NewRoleId,
    bool NewRoleBuiltIn,
    string? TenantName = null);
