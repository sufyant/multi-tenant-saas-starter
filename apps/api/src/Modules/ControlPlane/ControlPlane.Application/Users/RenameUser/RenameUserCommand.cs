using SharedKernel;

namespace ControlPlane.Application.Users.RenameUser;

/// <summary>A system admin changes a catalog user's names, in the catalog only (A7). It runs without a tenant.</summary>
[MessageName("rename-user")]
public sealed record RenameUserCommand(string ActorId, Guid UserId, string FirstName, string LastName);
