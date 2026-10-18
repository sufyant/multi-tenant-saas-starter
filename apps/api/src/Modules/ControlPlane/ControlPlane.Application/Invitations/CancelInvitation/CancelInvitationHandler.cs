using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace ControlPlane.Application.Invitations.CancelInvitation;

public static class CancelInvitationHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().RetryTimes(3);

    // An invitation already cancelled or accepted stays as it is: an owner who accepted before the cancel arrived keeps the tenant. The
    // invitation belongs to the tenant aggregate, so the tenant row is locked first (K6). A cancelled invitation's sign-up is withdrawn.
    public static async Task<WithdrawSignUpCommand?> HandleAsync(CancelInvitationCommand step, ITenantRepository tenants, CancellationToken cancellationToken)
    {
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("An invitation is cancelled in the tenant its creation added.");
        var invitation = await tenants.FindInvitationForUpdateAsync(step.InvitationId, cancellationToken);
        var cancelled = invitation?.Cancel() == StateChange.Changed;
        tenant.Summarize(await tenants.ReadRowsAsync(cancellationToken));
        await tenants.SaveChangesAsync(cancellationToken);
        return cancelled && invitation!.SignUpInvitationId is { } signUp ? new WithdrawSignUpCommand(signUp) : null;
    }
}
