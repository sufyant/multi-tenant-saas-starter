using SharedKernel;

namespace ControlPlane.Application.Users.RequestAccountDeletion;

/// <summary>
/// The signed-in user asks for the code that deletes their own account. ControlPlane sends it to the user's address. The endpoint has checked
/// first that the deletion may run (docs/flows/user-deletion.md, section 2). It runs without a tenant.
/// </summary>
[MessageName("request-account-deletion")]
public sealed record RequestAccountDeletionCommand(string ActorId);
