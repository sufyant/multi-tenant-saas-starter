using SharedKernel;

namespace ControlPlane.Application.Users.ReplaceIdentityEmail;

/// <summary>
/// Gives the identity provider the address that ControlPlane keeps for the user, after the commit of a change, so the person signs in
/// with it (A7). It carries the user, and the address the identity provider got last, if any: the step reads the address when it runs and
/// sends it on while the two differ, so two changes that reach the provider out of order still end on our record's address.
/// </summary>
[MessageName("replace-identity-email")]
public sealed record ReplaceIdentityEmailCommand(Guid UserId, string? Sent);
