using ControlPlane.Domain.EmailAddresses;

namespace ControlPlane.Application.SystemAdmins;

/// <summary>
/// The root system admins (section <c>ControlPlane:SystemAdmins</c>), checked on start: the email addresses of the people the system door
/// lets in as system admins, and whom no system admin can revoke (docs/flows/system-admins.md), and how fresh a second factor must be to
/// grant or revoke a system admin (A6). Public because a handler takes it (W9).
/// </summary>
public sealed class SystemAdminOptions
{
    public const string Section = "ControlPlane:SystemAdmins";

    /// <summary>The shortest reverification window Clerk allows.</summary>
    public static readonly TimeSpan MinReverificationWindow = TimeSpan.FromMinutes(1);

    /// <summary>The longest reverification window Clerk allows, and its strict level.</summary>
    public static readonly TimeSpan MaxReverificationWindow = TimeSpan.FromMinutes(10);

    /// <summary>The email addresses of the root system admins. The list must not be empty, so the platform always has a way in.</summary>
    public List<string> RootEmails { get; set; } = [];

    /// <summary>
    /// How long ago the second factor may have been verified for the grant and the revocation of a system admin: from 1 to 10 minutes, as
    /// Clerk's reverification allows; by default 10, Clerk's strict level (OWASP ASVS 4.3; Clerk, "Add reverification for sensitive
    /// actions").
    /// </summary>
    public TimeSpan ReverificationWindow { get; set; } = MaxReverificationWindow;

    /// <summary>The listed addresses in their one form. The check on start accepts only addresses, so none is left out.</summary>
    internal IReadOnlyList<EmailAddress> Roots => EmailAddress.AllOf(RootEmails);
}
