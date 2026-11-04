namespace ControlPlane.Application.Identity;

/// <summary>
/// The identity provider: it only signs people in. Who they are in a tenant, and their names, stay in our tables. It sends no
/// email and keeps no name; an invitation stays ours, and the provider's invitation only lets the invited person sign up.
/// </summary>
public interface IIdentityProvider
{
    /// <summary>
    /// Opens the sign-up for the email until the expiry, and answers the link the invitation email carries. The link leads to the accept link
    /// once the person has an account. An email that already has an account needs no sign-up: the answer is the accept link itself,
    /// without an invitation id.
    /// </summary>
    Task<SignUpLink> OpenSignUpAsync(string email, Uri acceptLink, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    /// <summary>Withdraws the permission to sign up. An invitation that is no longer pending, or that the provider does not know, is done.</summary>
    Task WithdrawSignUpAsync(string invitationId, CancellationToken cancellationToken);

    /// <summary>The email addresses the user has verified.</summary>
    Task<IReadOnlyList<string>> FindVerifiedEmailsAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the user's addresses with this one, verified and primary, because ControlPlane owns the address (A7). A linked Google or
    /// Microsoft account stays a sign-in method. An address the user has already is kept, so a second call is done.
    /// </summary>
    Task ReplaceEmailAsync(string externalUserId, string email, CancellationToken cancellationToken);

    /// <summary>
    /// Whether an account other than this user's has the address at the identity provider, verified or not, such as a person who signed up
    /// and never joined. A change of address refuses it, because the provider would refuse it for good (A7).
    /// </summary>
    Task<bool> IsEmailTakenByAnotherAccountAsync(string email, string externalUserId, CancellationToken cancellationToken);

    /// <summary>Deletes the user. A user the provider does not know is done.</summary>
    Task DeleteUserAsync(string externalUserId, CancellationToken cancellationToken);
}
