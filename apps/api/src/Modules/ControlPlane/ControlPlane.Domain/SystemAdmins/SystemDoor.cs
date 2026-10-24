using ControlPlane.Domain.EmailAddresses;
using SharedKernel;

namespace ControlPlane.Domain.SystemAdmins;

/// <summary>The door system admins use (T5): only a system admin passes, and only with a second factor verified (A6).</summary>
internal static class SystemDoor
{
    /// <summary>The platform permissions the user holds behind the door, or null when the door stays closed to them.</summary>
    public static IReadOnlySet<string>? PermissionsFor(bool isSystemAdmin, bool secondFactorVerified) =>
        isSystemAdmin && secondFactorVerified ? PermissionPools.PlatformPool : null;

    /// <summary>
    /// Whether the second factor is fresh enough for an operation that requires reverification (A6): verified less than the window ago,
    /// as Clerk's own check counts it. A factor as old as the window, or none, is refused with one code and nothing more, so the client
    /// asks the person to verify again (OWASP ASVS 4.3; Clerk, "Add reverification for sensitive actions").
    /// </summary>
    public static Result CheckReverification(TimeSpan? secondFactorAge, TimeSpan window) =>
        secondFactorAge is { } age && age < window
            ? Result.Success()
            : Error.Forbidden(ControlPlaneErrorCodes.SystemAdminReverificationRequired, "Verify the second factor again.");

    /// <summary>
    /// The root address of the person at the door, or none (docs/flows/system-admins.md): one of the addresses the identity provider
    /// has verified for them is in the configuration's list, both in their one form, and the session verified a second factor.
    /// </summary>
    public static EmailAddress? RootEmailOf(IEnumerable<EmailAddress> rootEmails, bool secondFactorVerified, IEnumerable<EmailAddress> verifiedEmails) =>
        secondFactorVerified ? verifiedEmails.FirstOrDefault(rootEmails.Contains) : null;

    /// <summary>
    /// Whether the configuration's list names the user's address, in its one form. This is the one place that answers "is this system
    /// admin a root": root is read from the list on each check and stored nowhere, so an address taken out of the list lifts the
    /// protection at the next deploy.
    /// </summary>
    public static bool IsRoot(string? email, IEnumerable<EmailAddress> rootEmails) =>
        EmailAddress.TryCreate(email) is { } address && rootEmails.Contains(address);
}
