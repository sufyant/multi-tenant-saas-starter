namespace ControlPlane.Domain.Users;

/// <summary>
/// One identity across tenants, matched to the identity provider's user id. The user keeps a copy of their email address and name;
/// the identity provider owns these fields.
/// </summary>
internal sealed class User(Guid id, string externalId)
{
    public Guid Id { get; private init; } = id;

    public string ExternalId { get; private init; } = externalId;

    public string? Email { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    /// <summary>The identity provider's time of the change the copy holds.</summary>
    public DateTimeOffset? ProfileUpdatedAt { get; private set; }

    /// <summary>When the identity provider deleted the user. The row stays: memberships and audit entries point to it.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>When a system admin suspended the user. A suspended user is refused on every me and tenant endpoint (A9).</summary>
    public DateTimeOffset? SuspendedAt { get; private set; }

    /// <summary>
    /// Takes the identity provider's profile, unless the copy holds a later change or the user is deleted. The same change
    /// taken twice gives the same copy. True when the email address or the name changed; a newer time alone is no change.
    /// </summary>
    public bool CopyProfile(string? email, string? firstName, string? lastName, DateTimeOffset updatedAt)
    {
        if (DeletedAt is not null || updatedAt < ProfileUpdatedAt)
        {
            return false;
        }

        var changed = (Email, FirstName, LastName) != (email, firstName, lastName);
        (Email, FirstName, LastName, ProfileUpdatedAt) = (email, firstName, lastName, updatedAt);
        return changed;
    }

    /// <summary>Erases the copy and marks the user deleted. A second deletion keeps the first time. True for the first deletion.</summary>
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
