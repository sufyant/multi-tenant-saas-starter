namespace ControlPlane.Application.Invitations;

/// <summary>
/// The user who invited, as the invitation's email names them, read when the email's event is published: a system admin invites as the
/// platform (the preview's rule, <c>IUserQueries.FindInviterNameAsync</c>), any other user by the name they have at that moment. The time
/// zone is an IANA name, or null when it is not set.
/// </summary>
public sealed record InvitingUser(bool IsSystemAdmin, string? Name, string? TimeZone)
{
    /// <summary>
    /// The name the email writes: the user's whole name, or null for the platform, whose email names the brand. A user without a name,
    /// such as one deleted meanwhile, is named as the platform too, so no email says that nobody invited.
    /// </summary>
    public string? NameForEmail => IsSystemAdmin || string.IsNullOrWhiteSpace(Name) ? null : Name;
}
