using ControlPlane.Application.Users;

namespace ControlPlane.Application.Invitations.GetInvitationPreview;

/// <summary>
/// Who invited, as the accept screen names them: a user, by their names as they are stored now, or the platform, when a system
/// admin invited (the names of <c>InviterKind</c>, A14). The screen never sees a system admin's name.
/// </summary>
public sealed record InviterNameView(string Kind, UserNameView? Name);
