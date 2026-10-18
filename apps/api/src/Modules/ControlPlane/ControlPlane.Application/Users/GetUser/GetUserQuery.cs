using SharedKernel;

namespace ControlPlane.Application.Users.GetUser;

/// <summary>
/// One user, for a system admin: the user detail screen. It runs without a tenant, and declares the user to read their
/// memberships in every tenant (R11), as a user's deletion does.
/// </summary>
[MessageName("get-user")]
public sealed record GetUserQuery(Guid UserId);
