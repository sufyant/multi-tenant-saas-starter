using SharedKernel;

namespace ControlPlane.Application.Invitations.ExpireSignUp;

/// <summary>
/// Our invitation's lifetime is over: the identity provider's invitation, which counts its life in whole days, is withdrawn at the
/// same moment. Scheduled when the link goes out.
/// </summary>
[MessageName("expire-sign-up")]
public sealed record ExpireSignUpCommand(Guid TenantId, Guid InvitationId);
