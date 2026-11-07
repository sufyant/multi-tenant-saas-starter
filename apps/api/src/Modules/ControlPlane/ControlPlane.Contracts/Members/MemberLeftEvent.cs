using SharedKernel;

namespace ControlPlane.Contracts.Members;

/// <summary>
/// A member left the tenant by themselves; they lose access at once. Published through the outbox; a subscriber handles it in the
/// tenant's transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the member who left, who acted.</param>
/// <param name="Role">The name of the role the member had: <c>owner</c>, <c>admin</c>, <c>member</c> or a custom role's.</param>
/// <param name="RoleId">The id of that role.</param>
/// <param name="RoleBuiltIn">Whether that role is built in.</param>
[MessageName("member-left")]
public sealed record MemberLeftEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid UserId,
    string Role,
    Guid RoleId,
    bool RoleBuiltIn);
