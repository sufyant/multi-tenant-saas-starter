using SharedKernel;

namespace ControlPlane.Application.Roles.CreateRole;

/// <summary>
/// A member creates a custom role of the active tenant with a name and permissions of the tenant pool (A3). The host's idempotency layer
/// protects the HTTP request (I1); the command carries no request key, and the server chooses the role's id.
/// </summary>
[MessageName("create-role")]
public sealed record CreateRoleCommand(string ActorId, string Name, IReadOnlyList<string> Permissions);
