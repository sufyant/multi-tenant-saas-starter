using SharedKernel;

namespace ControlPlane.Application.Users.DeleteUser;

/// <summary>
/// The last step of a system admin's deletion of a user, once the user has left every tenant: the catalog erases the user's email address
/// and names and marks the user deleted (A8). The row stays, because audit entries point to it. The identity provider deletes its user
/// after the commit. It runs without a tenant, with the user declared (R11).
/// </summary>
[MessageName("delete-user")]
public sealed record DeleteUserCommand(string ActorId, Guid UserId);
