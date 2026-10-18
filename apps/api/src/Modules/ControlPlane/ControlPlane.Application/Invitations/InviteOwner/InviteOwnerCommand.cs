using SharedKernel;

namespace ControlPlane.Application.Invitations.InviteOwner;

/// <summary>
/// A system admin invites an Owner into the tenant the platform route names, which has no Owner because its first Owner never accepted,
/// in a language for the invitation's email: without one, the system admin's own (K8). The host's idempotency layer protects the HTTP
/// request (I1); the command carries no request key, and the server chooses the invitation's id.
/// </summary>
public sealed record InviteOwnerCommand(string ActorId, string Email, Language? Language);
