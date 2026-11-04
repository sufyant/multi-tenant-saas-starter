using ControlPlane.Application.Identity;

namespace ControlPlane.Application.Users.ReplaceIdentityEmail;

// The handler takes no DbContext, so Wolverine opens no transaction around the call (section 3). It has the retries of the calls to the
// identity provider (IdentityProviderRetries); once they are spent, the message goes to the dead letter queue, where the maintainer
// retries it (W13).
public static class ReplaceIdentityEmailHandler
{
    public static Task HandleAsync(ReplaceIdentityEmailCommand replace, IIdentityProvider identity, CancellationToken cancellationToken) =>
        identity.ReplaceEmailAsync(replace.ExternalUserId, replace.Email, cancellationToken);
}
