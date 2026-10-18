using ControlPlane.Application.Invitations.SignUp;
using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain.Tenants;
using Microsoft.Extensions.Logging;
using SharedKernel;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace ControlPlane.Application.Tenants.TenantCreation;

// The steps and compensations of the tenant creation saga. Each runs in the new tenant, may run twice (S7), and returns its
// result as a message. A step that fails for good goes to the dead letter queue, and the saga hears of it through the fault
// Wolverine publishes.

public static class ActivateTenantHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().RetryTimes(3);

    // The pivot. The tenant is locked, so a repeated activation finds it active and announces nothing again. The tenant was created
    // by the system admin who invited its first owner. The saga hears of it through a reply of its own (S13). Step 3 starts here: the
    // identity provider lets the owner sign up, then Notifications sends the email.
    public static async Task<(ActivateTenantReply?, TenantActivatedEvent?, OpenOwnerSignUpCommand?)> HandleAsync(
        ActivateTenantCommand step,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await catalog.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("A tenant is activated after its creation added it.");
        if (tenant.Activate() == StateChange.Unchanged)
        {
            return (null, null, null);
        }

        var invitation = await catalog.FindInvitationForUpdateAsync(step.InvitationId, cancellationToken)
            ?? throw new InvalidOperationException("A tenant is activated with its first owner's invitation.");
        var now = time.GetUtcNow();
        await catalog.SaveChangesAsync(cancellationToken);

        return (
            new ActivateTenantReply(tenant.Id),
            new TenantActivatedEvent(Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, invitation.InvitedBy),
            new OpenOwnerSignUpCommand(tenant.Id, invitation.Id, invitation.Email, tenant.Name, step.AcceptLink, invitation.ExpiresAt));
    }
}

public static partial class CancelTenantHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().RetryTimes(3);

    // An active or suspended tenant is past the pivot and is never cancelled: the compensation changes nothing and reports nothing, and
    // the saga's cancellation timeout leaves the tenant creation to a person. A cancelled tenant keeps its first reason.
    public static async Task<CancelTenantReply?> HandleAsync(
        CancelTenantCommand step,
        ITenantCatalog catalog,
        ILogger<TenantCreationSaga> logger,
        CancellationToken cancellationToken)
    {
        var tenant = await catalog.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("A tenant is cancelled after its creation added it.");
        if (tenant.Status is TenantStatus.Active or TenantStatus.Suspended)
        {
            return null;
        }

        if (tenant.Cancel(step.Reason) == StateChange.Changed)
        {
            LogCancelled(logger, step.TenantId, step.Reason);
        }

        (await catalog.FindInvitationForUpdateAsync(step.InvitationId, cancellationToken))?.Cancel();
        tenant.Summarize(await catalog.ReadRowsAsync(cancellationToken));
        await catalog.SaveChangesAsync(cancellationToken);
        return new CancelTenantReply(step.TenantId);
    }

    [LoggerMessage(EventName = "TenantCancelled", Level = LogLevel.Warning, Message = "Tenant {TenantId} is cancelled: {Reason}")]
    private static partial void LogCancelled(ILogger logger, Guid tenantId, string reason);
}

public static class CancelInvitationHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().RetryTimes(3);

    // An invitation already cancelled or accepted stays as it is: an owner who accepted before the cancel arrived keeps the tenant. The
    // invitation belongs to the tenant aggregate, so the tenant row is locked first (K6). A cancelled invitation's sign-up is withdrawn.
    public static async Task<WithdrawSignUpCommand?> HandleAsync(CancelInvitationCommand step, ITenantCatalog catalog, CancellationToken cancellationToken)
    {
        var tenant = await catalog.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("An invitation is cancelled in the tenant its creation added.");
        var invitation = await catalog.FindInvitationForUpdateAsync(step.InvitationId, cancellationToken);
        var cancelled = invitation?.Cancel() == StateChange.Changed;
        tenant.Summarize(await catalog.ReadRowsAsync(cancellationToken));
        await catalog.SaveChangesAsync(cancellationToken);
        return cancelled && invitation!.SignUpInvitationId is { } signUp ? new WithdrawSignUpCommand(signUp) : null;
    }
}

public static class RaiseTenantCreationAlarmHandler
{
    public static void Handle(RaiseTenantCreationAlarmCommand alarm, TenantCreationAlarm tenantCreationAlarm) => tenantCreationAlarm.Raise(alarm.TenantId);
}
