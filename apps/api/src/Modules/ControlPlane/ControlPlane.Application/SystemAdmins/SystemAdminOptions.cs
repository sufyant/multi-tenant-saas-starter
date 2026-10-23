using ControlPlane.Domain.EmailAddresses;

namespace ControlPlane.Application.SystemAdmins;

/// <summary>
/// The root system admins (section <c>ControlPlane:SystemAdmins</c>), checked on start: the email addresses of the people the system door
/// lets in as system admins, and whom no system admin can revoke (section 6, "Root system admins"). Public because a handler takes it (W9).
/// </summary>
public sealed class SystemAdminOptions
{
    public const string Section = "ControlPlane:SystemAdmins";

    /// <summary>The email addresses of the root system admins. The list must not be empty, so the platform always has a way in.</summary>
    public List<string> RootEmails { get; set; } = [];

    /// <summary>The listed addresses in their one form. The check on start accepts only addresses, so none is left out.</summary>
    internal IReadOnlyList<EmailAddress> Roots => EmailAddress.AllOf(RootEmails);
}
