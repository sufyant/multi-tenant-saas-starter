using SharedKernel;

namespace ControlPlane.Application.Users.RenameMe;

/// <summary>
/// The signed-in user corrects their own name, in ControlPlane only (A7). It runs without a tenant. The token names the user, so a
/// user changes only their own name.
/// </summary>
[MessageName("rename-me")]
public sealed record RenameMeCommand(string ActorId, string Name);
