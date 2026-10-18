using SharedKernel;

namespace ControlPlane.Application.Invitations.CancelInvitation;

/// <summary>Cancels the owner's invitation, when nobody could be told of it.</summary>
[MessageName("cancel-invitation")]
public sealed record CancelInvitationCommand(Guid TenantId, Guid InvitationId);
