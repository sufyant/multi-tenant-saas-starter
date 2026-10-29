using SharedKernel;

namespace ControlPlane.Contracts.Members;

/// <summary>
/// A user accepted an invitation and became a member of the tenant. Published through the outbox; a subscriber handles it in the
/// tenant's transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the new member, who acted.</param>
/// <param name="TenantName">The tenant's name at that moment, for a notification. None in an event of the version before step 33, which
/// the new fields follow so that both versions read each other's events.</param>
/// <param name="InvitedBy">The user id of the member who invited them; none when the platform invited, and in an event of the version
/// before step 33.</param>
/// <param name="FirstOwner">Whether they joined as an Owner of a tenant that had none: a new tenant's first Owner.</param>
[MessageName("member-joined")]
public sealed record MemberJoinedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid UserId,
    string? TenantName = null,
    Guid? InvitedBy = null,
    bool FirstOwner = false);
