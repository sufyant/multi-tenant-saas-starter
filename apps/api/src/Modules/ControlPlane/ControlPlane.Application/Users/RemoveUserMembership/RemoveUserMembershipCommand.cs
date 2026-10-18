using SharedKernel;

namespace ControlPlane.Application.Users.RemoveUserMembership;

/// <summary>
/// A step of a user's deletion: the user leaves one tenant. It runs in that tenant, which the system admin's deletion names.
/// </summary>
[MessageName("remove-user-membership")]
public sealed record RemoveUserMembershipCommand(Guid UserId);
