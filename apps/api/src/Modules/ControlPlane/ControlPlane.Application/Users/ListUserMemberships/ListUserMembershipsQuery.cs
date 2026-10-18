using SharedKernel;

namespace ControlPlane.Application.Users.ListUserMemberships;

/// <summary>
/// The first step of a user's deletion: the tenants the user is a member of, and whether they are an Owner there. It runs without a
/// tenant, with the user declared (R11), so it sees the user's own memberships in every tenant.
/// </summary>
[MessageName("list-user-memberships")]
public sealed record ListUserMembershipsQuery(Guid UserId);
