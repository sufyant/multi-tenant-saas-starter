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

    /// <summary>
    /// Takes the identity provider's profile, unless the copy holds a later change or the user is deleted. The same change
    /// taken twice gives the same copy.
    /// </summary>
    public void CopyProfile(string? email, string? firstName, string? lastName, DateTimeOffset updatedAt)
    {
        if (DeletedAt is not null || updatedAt < ProfileUpdatedAt)
        {
            return;
        }

        (Email, FirstName, LastName, ProfileUpdatedAt) = (email, firstName, lastName, updatedAt);
    }

    /// <summary>Erases the copy and marks the user deleted. A second deletion keeps the first time.</summary>
    public void Delete(DateTimeOffset now)
    {
        (Email, FirstName, LastName) = (null, null, null);
        DeletedAt ??= now;
    }

    public DateTimeOffset? SuspendedAt { get; private set; }

    public void Suspend(DateTimeOffset now) => throw new NotImplementedException();

    public void Reactivate() => throw new NotImplementedException();
}
