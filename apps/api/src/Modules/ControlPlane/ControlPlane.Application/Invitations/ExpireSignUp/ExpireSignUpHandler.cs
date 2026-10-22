using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;

namespace ControlPlane.Application.Invitations.ExpireSignUp;

// The invitation's lifetime is over. An invitation still pending keeps its identity provider's invitation until now, so it is withdrawn;
// a revoked, replaced or cancelled one withdrew it when it ended, and an accepted one no longer needs it. The message is scheduled by the
// messaging tool's clock; the module's clock decides whether the lifetime is over, so a message that arrives early withdraws nothing
// (Invitation.SignUpToWithdrawAt).
public static class ExpireSignUpHandler
{
    public static async Task<WithdrawSignUpCommand?> HandleAsync(
        ExpireSignUpCommand expire,
        ITenantRepository tenants,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        _ = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("An invitation's sign-up runs in the tenant of the invitation.");
        var invitation = await tenants.FindInvitationForUpdateAsync(expire.InvitationId, cancellationToken);
        return invitation?.SignUpToWithdrawAt(time.GetUtcNow()) is { } signUp ? new WithdrawSignUpCommand(signUp) : null;
    }
}
