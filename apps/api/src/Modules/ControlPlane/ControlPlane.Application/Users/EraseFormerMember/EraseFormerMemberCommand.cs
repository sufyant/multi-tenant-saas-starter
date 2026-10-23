using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Users.EraseFormerMember;

/// <summary>
/// A step of a user's deletion, after the user's erasure: a tenant the user left before erases the user's address from its invitations
/// (A8; docs/flows/user-deletion.md). It runs in that tenant. The address is the user's, read before the erasure, or none when the user
/// was already deleted.
/// </summary>
[MessageName("erase-former-member")]
public sealed record EraseFormerMemberCommand(Guid UserId, string? Email)
{
    /// <summary>One erasure in each tenant where the user accepted an invitation, each delivered in its tenant (R4).</summary>
    internal static IEnumerable<DeliveryMessage<EraseFormerMemberCommand>> For(Guid userId, string? email, IEnumerable<Guid> tenantIds) =>
        tenantIds.Select(tenantId => new EraseFormerMemberCommand(userId, email).WithDeliveryOptions(new DeliveryOptions { TenantId = tenantId.ToString() }));
}
