namespace ControlPlane.Application.Ports;

/// <summary>The identity provider (Clerk): it authenticates people; who they are in a tenant stays in our catalog.</summary>
public interface IIdentityProvider
{
    /// <summary>The user's verified email addresses and their profile, in one call.</summary>
    Task<IdentityAccount> FindAccountAsync(string externalUserId, CancellationToken cancellationToken);
}

/// <summary>A user as the identity provider knows them: the email addresses they have verified, and their profile.</summary>
public sealed record IdentityAccount(IReadOnlyList<string> VerifiedEmails, UserProfile Profile);

/// <summary>
/// The fields of a user that the catalog keeps a copy of. The identity provider owns them. <see cref="UpdatedAt"/> is the provider's
/// time of the user's last change.
/// </summary>
public sealed record UserProfile(string? Email, string? FirstName, string? LastName, DateTimeOffset UpdatedAt);
