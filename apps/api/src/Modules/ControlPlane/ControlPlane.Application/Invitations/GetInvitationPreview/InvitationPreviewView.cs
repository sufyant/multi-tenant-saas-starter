using ControlPlane.Application.Members;

namespace ControlPlane.Application.Invitations.GetInvitationPreview;

/// <summary>
/// The tenant's name, the inviter, the role the invitation gives, its expiry, and its state at the
/// moment of the request (<c>Pending</c>, <c>Expired</c>, <c>Accepted</c>, <c>Cancelled</c>).
/// </summary>
public sealed record InvitationPreviewView(string TenantName, InviterNameView Inviter, RoleView Role, DateTimeOffset ExpiresAt, string State);
