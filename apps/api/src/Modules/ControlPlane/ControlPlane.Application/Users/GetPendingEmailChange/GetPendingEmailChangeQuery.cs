using SharedKernel;

namespace ControlPlane.Application.Users.GetPendingEmailChange;

/// <summary>The address of the signed-in user's latest code of a change of address, which the confirmation checks first; none without a code.</summary>
[MessageName("get-pending-email-change")]
public sealed record GetPendingEmailChangeQuery(string ActorId);
