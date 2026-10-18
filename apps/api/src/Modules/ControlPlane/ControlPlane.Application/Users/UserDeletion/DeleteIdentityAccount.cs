using ControlPlane.Application.Ports;

namespace ControlPlane.Application.Users.UserDeletion;

/// <summary>Deletes the user at the identity provider, after the catalog erased them, so the person can no longer sign in.</summary>
public sealed record DeleteIdentityAccountCommand(string ExternalUserId);

// The handler takes no DbContext, so Wolverine opens no transaction around the call (section 3). It has the retries of the calls to
// the identity provider (IdentityProviderRetries); once they are spent, the message goes to the dead letter queue, and a second deletion
// of the user asks again.
public static class DeleteIdentityAccountHandler
{
    public static Task HandleAsync(DeleteIdentityAccountCommand delete, IIdentityProvider identity, CancellationToken cancellationToken) =>
        identity.DeleteUserAsync(delete.ExternalUserId, cancellationToken);
}
