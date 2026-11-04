using SharedKernel;

namespace ControlPlane.Application.Users.ChangeEmail;

/// <summary>
/// The signed-in user gives the code that went to their new address: ControlPlane changes the address, and the identity provider takes it
/// after the commit (A7). It runs without a tenant.
/// </summary>
[MessageName("change-email")]
public sealed record ChangeEmailCommand(string ActorId, string Code);
