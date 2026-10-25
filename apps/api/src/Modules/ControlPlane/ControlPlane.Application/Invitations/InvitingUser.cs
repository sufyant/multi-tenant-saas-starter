namespace ControlPlane.Application.Invitations;

/// <summary>
/// The user who invited, as the invitation's email names them, read when the email's event is published: a system admin invites as the
/// platform (the preview's rule, <c>IUserQueries.FindInviterNameAsync</c>), any other user by the names they have at that moment. The time
/// zone is an IANA name, or null when it is not set.
/// </summary>
public sealed record InvitingUser(bool IsSystemAdmin, string? FirstName, string? LastName, string? TimeZone)
{
    /// <summary>
    /// The name the email writes: the first and the last name, or null for the platform, whose email names the brand. A user without
    /// names, such as one deleted meanwhile, is named as the platform too, so no email says that nobody invited.
    /// </summary>
    public string? NameForEmail
    {
        get
        {
            var name = string.Join(' ', new[] { FirstName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
            return IsSystemAdmin || name.Length == 0 ? null : name;
        }
    }
}
