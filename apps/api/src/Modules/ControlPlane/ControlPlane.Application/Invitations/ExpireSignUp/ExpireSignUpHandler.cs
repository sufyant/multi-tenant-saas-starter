using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Ports;
using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Invitations.ExpireSignUp;

// The invitation's lifetime is over. An invitation still pending keeps its identity provider's invitation until now, so it is withdrawn;
// a revoked, replaced or cancelled one withdrew it when it ended, and an accepted one no longer needs it. The message is scheduled by the
// messaging tool's clock; the module's clock decides whether the lifetime is over, so a message that arrives early withdraws nothing.
public static class ExpireSignUpHandler
{
    public static async Task<WithdrawSignUpCommand?> HandleAsync(
        ExpireSignUpCommand expire,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        _ = await catalog.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("An invitation's sign-up runs in the tenant of the invitation.");
        var invitation = await catalog.FindInvitationForUpdateAsync(expire.InvitationId, cancellationToken);
        return invitation is { Status: InvitationStatus.Pending, SignUpInvitationId: { } signUp } && time.GetUtcNow() >= invitation.ExpiresAt
            ? new WithdrawSignUpCommand(signUp)
            : null;
    }
}
