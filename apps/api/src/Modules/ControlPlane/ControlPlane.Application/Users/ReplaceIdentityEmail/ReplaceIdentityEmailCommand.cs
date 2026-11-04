using SharedKernel;

namespace ControlPlane.Application.Users.ReplaceIdentityEmail;

/// <summary>
/// Replaces the user's address at the identity provider with the one ControlPlane now keeps, after the commit, so the person signs in with
/// it (A7). A linked Google or Microsoft account stays a sign-in method.
/// </summary>
[MessageName("replace-identity-email")]
public sealed record ReplaceIdentityEmailCommand(string ExternalUserId, string Email);
