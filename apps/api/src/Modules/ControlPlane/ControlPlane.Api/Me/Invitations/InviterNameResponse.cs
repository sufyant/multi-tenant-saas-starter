namespace ControlPlane.Api.Me.Invitations;

/// <summary>
/// Who invited: a user, with the name stored now and <c>deleted</c> for a user a system admin deleted, or the platform,
/// with no name and no <c>deleted</c>. The screen names the platform from <c>kind</c>, as the audit logs do.
/// </summary>
public sealed record InviterNameResponse(InviterNameResponseKind Kind, string? Name, bool? Deleted);
