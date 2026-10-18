using SharedKernel;

namespace ControlPlane.Api.Platform.Invitations;

/// <summary>
/// The address of the Owner the platform invites into a tenant without one, and the language of the invitation's email: without one, the
/// system admin's own.
/// </summary>
public sealed record InviteOwnerRequest(string Email, Language? Language = null);
