using ControlPlane.Application.Invitations.OpenOwnerSignUp;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace ControlPlane.Application.Tenants.ActivateTenant;

public static class ActivateTenantHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().RetryTimes(3);

    // The pivot. The tenant is locked, so a repeated activation finds it active and announces nothing again. The tenant was created
    // by the system admin who invited its first owner. The saga hears of it through a reply of its own (S13). Step 3 starts here: the
    // identity provider lets the owner sign up, then Notifications sends the email.
    public static async Task<(ActivateTenantReply?, TenantActivatedEvent?, OpenOwnerSignUpCommand?)> HandleAsync(
        ActivateTenantCommand step,
        ITenantRepository tenants,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("A tenant is activated after its creation added it.");
        if (tenant.Activate() == StateChange.Unchanged)
        {
            return (null, null, null);
        }

        var invitation = await tenants.FindInvitationForUpdateAsync(step.InvitationId, cancellationToken)
            ?? throw new InvalidOperationException("A tenant is activated with its first owner's invitation.");
        var now = time.GetUtcNow();
        await tenants.SaveChangesAsync(cancellationToken);

        return (
            new ActivateTenantReply(tenant.Id),
            new TenantActivatedEvent(Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, invitation.InvitedBy),
            new OpenOwnerSignUpCommand(tenant.Id, invitation.Id, invitation.Email, tenant.Name, step.AcceptLink, invitation.ExpiresAt));
    }
}
