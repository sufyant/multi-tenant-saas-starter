using SharedKernel;

namespace ControlPlane.Contracts.Members;

/// <summary>
/// A system admin made a member of a tenant an Owner (Owner recovery). Published through the outbox; Audit records it in the platform's
/// log, about the tenant, and in the tenant's log, as the member's role change, as the tenant's id travels in the message's envelope (K3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="TenantName">The tenant's name at that moment, which the platform's record keeps.</param>
/// <param name="UserId">The user id of the member who became an Owner.</param>
/// <param name="MadeBy">The user id of the system admin who made them an Owner, who acted.</param>
/// <param name="OldRole">The name of the role the member had: <c>admin</c>, <c>member</c> or a custom role's.</param>
/// <param name="OldRoleId">The id of the role the member had.</param>
/// <param name="OldRoleBuiltIn">Whether the role the member had is built in.</param>
/// <param name="NewRole">The name of the tenant's Owner role, <c>owner</c>.</param>
/// <param name="NewRoleId">The id of the tenant's Owner role.</param>
[MessageName("owner-made")]
public sealed record OwnerMadeEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid UserId,
    Guid MadeBy,
    string OldRole,
    Guid OldRoleId,
    bool OldRoleBuiltIn,
    string NewRole,
    Guid NewRoleId);
