using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain.Tenants;
using Microsoft.Extensions.Logging;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace ControlPlane.Application.Tenants;

// The steps and compensations of the tenant creation saga. Each runs in the new tenant, may run twice (S7), and returns its
// result as a message. A step that fails for good goes to the dead letter queue, and the saga hears of it through the fault
// Wolverine publishes.

public static class ActivateTenantHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().RetryTimes(3);

    // The pivot. The tenant is locked, so a repeated activation finds it active and announces nothing again. The tenant was created
    // by the system admin who invited its first owner. The saga hears of it through a reply of its own (S13).
    public static async Task<(ActivateTenantReply?, TenantActivated?, OwnerInvitationReady?)> HandleAsync(
        ActivateTenant step,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await catalog.FindTenantForUpdateAsync(cancellationToken);
        if (!tenant.Activate())
        {
            return (null, null, null);
        }

        var invitation = await catalog.FindInvitationForUpdateAsync(step.InvitationId, cancellationToken)
            ?? throw new InvalidOperationException("A tenant is activated with its first owner's invitation.");
        var now = time.GetUtcNow();
        await catalog.SaveChangesAsync(cancellationToken);

        return (
            new ActivateTenantReply(tenant.Id),
            new TenantActivated(Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, invitation.InvitedBy),
            new OwnerInvitationReady(Guid.CreateVersion7(now), now, tenant.Id, invitation.Id, invitation.Email, tenant.Name, step.Link));
    }
}

public static partial class CancelTenantHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().RetryTimes(3);

    // An active tenant is past the pivot and is never cancelled: the compensation changes nothing and reports nothing, and the saga's
    // cancellation timeout leaves the tenant creation to a person. A cancelled tenant keeps its first reason.
    public static async Task<CancelTenantReply?> HandleAsync(
        CancelTenant step,
        ITenantCatalog catalog,
        ILogger<TenantCreationSaga> logger,
        CancellationToken cancellationToken)
    {
        var tenant = await catalog.FindTenantForUpdateAsync(cancellationToken);
        if (tenant.Status == TenantStatus.Active)
        {
            return null;
        }

        if (tenant.Cancel(step.Reason))
        {
            LogCancelled(logger, step.TenantId, step.Reason);
        }

        (await catalog.FindInvitationForUpdateAsync(step.InvitationId, cancellationToken))?.Cancel();
        await catalog.SaveChangesAsync(cancellationToken);
        return new CancelTenantReply(step.TenantId);
    }

    [LoggerMessage(EventName = "TenantCancelled", Level = LogLevel.Warning, Message = "Tenant {TenantId} is cancelled: {Reason}")]
    private static partial void LogCancelled(ILogger logger, Guid tenantId, string reason);
}

public static class CancelInvitationHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().RetryTimes(3);

    // An invitation already cancelled or accepted stays as it is: an owner who accepted before the cancel arrived keeps the tenant.
    public static async Task HandleAsync(CancelInvitation step, ITenantCatalog catalog, CancellationToken cancellationToken)
    {
        (await catalog.FindInvitationForUpdateAsync(step.InvitationId, cancellationToken))?.Cancel();
        await catalog.SaveChangesAsync(cancellationToken);
    }
}

public static class TenantCreationAlarmHandler
{
    public static void Handle(RaiseTenantCreationAlarm alarm, TenantCreationAlarm tenantCreationAlarm) => tenantCreationAlarm.Raise(alarm.TenantId);
}
