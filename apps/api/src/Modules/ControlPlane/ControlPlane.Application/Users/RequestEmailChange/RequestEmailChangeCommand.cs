using SharedKernel;

namespace ControlPlane.Application.Users.RequestEmailChange;

/// <summary>
/// The signed-in user asks to change their address: ControlPlane sends a code to the new address, our email, not the identity provider's
/// (A7). It runs without a tenant.
/// </summary>
[MessageName("request-email-change")]
public sealed record RequestEmailChangeCommand(string ActorId, string Email);
