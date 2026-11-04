using SharedKernel;

namespace ControlPlane.Application.Users.SendIdentityEmail;

/// <summary>Replaces the user's address at the identity provider with the one our record kept when the step before read it (A7).</summary>
[MessageName("send-identity-email")]
public sealed record SendIdentityEmailCommand(Guid UserId, string ExternalUserId, string Email);
