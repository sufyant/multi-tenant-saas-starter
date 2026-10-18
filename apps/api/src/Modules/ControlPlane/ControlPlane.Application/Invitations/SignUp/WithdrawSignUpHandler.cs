using ControlPlane.Application.Ports;

namespace ControlPlane.Application.Invitations.SignUp;

// Withdraws the identity provider's invitation, without a transaction, with the retries of the invitation email.
public static class WithdrawSignUpHandler
{
    public static Task HandleAsync(WithdrawSignUpCommand withdraw, IIdentityProvider identity, CancellationToken cancellationToken) =>
        identity.WithdrawSignUpAsync(withdraw.SignUpInvitationId, cancellationToken);
}
