using SharedKernel;

namespace ControlPlane.Application.Members.RemoveMember;

/// <summary>An Owner or an Admin removes a member from the active tenant; the member loses access at once (K1).</summary>
[MessageName("remove-member")]
public sealed record RemoveMemberCommand(string ActorId, Guid UserId);
