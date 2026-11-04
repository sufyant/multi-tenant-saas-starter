using SharedKernel;

namespace ControlPlane.Application.Users.ConfirmAccountDeletion;

/// <summary>
/// The first step of a user's deletion of their own account: the code that went to their address. With the right code, the endpoint runs
/// the steps of a user's deletion (docs/flows/user-deletion.md). It runs without a tenant.
/// </summary>
[MessageName("confirm-account-deletion")]
public sealed record ConfirmAccountDeletionCommand(string ActorId, string Code);
