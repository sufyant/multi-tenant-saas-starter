using ControlPlane.Application.Identity;

namespace ControlPlane.Application.Users.DeleteIdentityAccount;

// The handler takes no DbContext, so Wolverine opens no transaction around the call (section 3). It has the retries of the calls to
// the identity provider (IdentityProviderRetries); once they are spent, the message goes to the dead letter queue, and a second deletion
// of the user asks again.
public static class DeleteIdentityAccountHandler
{
    public static Task HandleAsync(DeleteIdentityAccountCommand delete, IIdentityProvider identity, CancellationToken cancellationToken) =>
        identity.DeleteUserAsync(delete.ExternalUserId, cancellationToken);
}
