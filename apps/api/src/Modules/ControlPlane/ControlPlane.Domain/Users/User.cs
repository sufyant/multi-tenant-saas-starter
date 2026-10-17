using SharedKernel;

namespace ControlPlane.Domain.Users;

/// <summary>
/// One identity across tenants, matched to the identity provider's user id. The catalog owns the email address and the names (A7):
/// the identity provider keeps no name, and only a system admin changes one.
/// </summary>
internal sealed class User(Guid id, string externalId)
{
    public const int NameMaxLength = 100;

    public Guid Id { get; private init; } = id;

    public string ExternalId { get; private init; } = externalId;

    public string? Email { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    /// <summary>When a system admin deleted the user. The row stays: audit entries point to it.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>When a system admin suspended the user. A suspended user is refused on every me and tenant endpoint (A9).</summary>
    public DateTimeOffset? SuspendedAt { get; private set; }

    /// <summary>
    /// A person the catalog does not know joins through an invitation: the invited address, which the identity provider verified, and
    /// the names they typed, without the spaces around them.
    /// </summary>
    public static User Join(Guid id, string externalId, string email, string firstName, string lastName) =>
        new(id, externalId) { Email = email, FirstName = firstName.Trim(), LastName = lastName.Trim() };

    /// <summary>
    /// A system admin changes the names. True when they changed; the same names with spaces around them are no change. A deleted user
    /// has no names to change: giving them one would keep personal data the deletion erased.
    /// </summary>
    public Result<bool> Rename(string firstName, string lastName)
    {
        if (DeletedAt is not null)
        {
            return Error.Conflict(ControlPlaneErrorCodes.UserDeleted, "The user is deleted.");
        }

        var changed = (FirstName, LastName) != (firstName.Trim(), lastName.Trim());
        (FirstName, LastName) = (firstName.Trim(), lastName.Trim());
        return changed;
    }

    /// <summary>Erases the email address and the names and marks the user deleted. A second deletion keeps the first time. True for the first deletion.</summary>
    public bool Delete(DateTimeOffset now)
    {
        var first = DeletedAt is null;
        (Email, FirstName, LastName) = (null, null, null);
        DeletedAt ??= now;
        return first;
    }

    /// <summary>
    /// Suspends the user. A second suspension keeps the first time. The identity provider is not told. True when the user was not
    /// suspended.
    /// </summary>
    public bool Suspend(DateTimeOffset now)
    {
        var changed = SuspendedAt is null;
        SuspendedAt ??= now;
        return changed;
    }

    /// <summary>Reactivates the user. True when the user was suspended.</summary>
    public bool Reactivate()
    {
        var changed = SuspendedAt is not null;
        SuspendedAt = null;
        return changed;
    }
}
