using ControlPlane.Application.Ports;

namespace ControlPlane.Application.Invitations.SignUp;

// Asks the identity provider to let a new tenant's first owner sign up, after the invitation committed. The handler takes no DbContext, so Wolverine opens
// no transaction around the call (section 3); it answers with a message, and a handler with a transaction records it. The calls have the
// retries of the invitation email (IdentityProviderRetries); once they are spent, the message goes to the dead letter queue, no email goes
// out, and our invitation stays as it is.
public static class OpenOwnerSignUpHandler
{
    public static async Task<RecordSignUpCommand> HandleAsync(OpenOwnerSignUpCommand open, IIdentityProvider identity, CancellationToken cancellationToken)
    {
        var signUp = await identity.AllowSignUpAsync(open.Email, open.AcceptLink, open.ExpiresAt, cancellationToken);
        return new RecordSignUpCommand(
            open.TenantId, open.InvitationId, open.Email, open.TenantName, signUp.Link, signUp.InvitationId, open.ExpiresAt, ForFirstOwner: true);
    }
}
