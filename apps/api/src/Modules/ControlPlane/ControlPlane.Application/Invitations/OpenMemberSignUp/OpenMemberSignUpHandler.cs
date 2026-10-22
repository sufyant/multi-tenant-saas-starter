using ControlPlane.Application.Identity;
using ControlPlane.Application.Invitations.RecordSignUp;
using Wolverine;

namespace ControlPlane.Application.Invitations.OpenMemberSignUp;

// Asks the identity provider to let the address a member invited sign up, after the invitation committed. The handler takes no DbContext, so Wolverine opens
// no transaction around the call (section 3); it answers with a message, and a handler with a transaction records it. The calls have the
// retries of the invitation email (IdentityProviderRetries); once they are spent, the message goes to the dead letter queue, no email goes
// out, and our invitation stays as it is.
public static class OpenMemberSignUpHandler
{
    public static async Task<OutgoingMessages> HandleAsync(OpenMemberSignUpCommand open, IIdentityProvider identity, CancellationToken cancellationToken)
    {
        var signUp = await identity.OpenSignUpAsync(open.Email, open.AcceptLink, open.ExpiresAt, cancellationToken);
        return
        [
            new RecordSignUpCommand(
                open.TenantId, open.InvitationId, open.Email, open.TenantName, signUp.Link, signUp.InvitationId, open.ExpiresAt, ForFirstOwner: false),
        ];
    }
}
