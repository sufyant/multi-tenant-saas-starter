using ControlPlane.Application.Ports;

namespace ControlPlane.Application.Invitations.SignUp;

// Asks the identity provider to let the invited address sign up, after the invitation committed. The handler takes no DbContext, so
// Wolverine opens no transaction around the call (section 3); it answers with a message, and a handler with a transaction records it.
// The calls have the retries of the invitation email (IdentityProviderRetries); once they are spent, the message goes to the dead letter
// queue, no email goes out, and our invitation stays as it is.
public static class OpenSignUpHandler
{
    public static Task<SignUpOpened> HandleAsync(OpenOwnerSignUp open, IIdentityProvider identity, CancellationToken cancellationToken) =>
        OpenAsync(open.TenantId, open.InvitationId, open.Email, open.TenantName, open.AcceptLink, open.ExpiresAt, forFirstOwner: true, identity, cancellationToken);

    public static Task<SignUpOpened> HandleAsync(OpenMemberSignUp open, IIdentityProvider identity, CancellationToken cancellationToken) =>
        OpenAsync(open.TenantId, open.InvitationId, open.Email, open.TenantName, open.AcceptLink, open.ExpiresAt, forFirstOwner: false, identity, cancellationToken);

    private static async Task<SignUpOpened> OpenAsync(
        Guid tenantId,
        Guid invitationId,
        string email,
        string tenantName,
        Uri acceptLink,
        DateTimeOffset expiresAt,
        bool forFirstOwner,
        IIdentityProvider identity,
        CancellationToken cancellationToken)
    {
        var signUp = await identity.AllowSignUpAsync(email, acceptLink, expiresAt, cancellationToken);
        return new SignUpOpened(tenantId, invitationId, email, tenantName, signUp.Link, signUp.InvitationId, expiresAt, forFirstOwner);
    }
}

// Withdraws the identity provider's invitation, without a transaction, with the retries of the invitation email.
public static class WithdrawSignUpHandler
{
    public static Task HandleAsync(WithdrawSignUp withdraw, IIdentityProvider identity, CancellationToken cancellationToken) =>
        identity.WithdrawSignUpAsync(withdraw.SignUpInvitationId, cancellationToken);
}
