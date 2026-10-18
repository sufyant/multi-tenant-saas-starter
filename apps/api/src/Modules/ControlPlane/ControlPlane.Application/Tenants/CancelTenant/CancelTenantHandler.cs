using ControlPlane.Application.Ports;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Domain.Tenants;
using Microsoft.Extensions.Logging;
using SharedKernel;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace ControlPlane.Application.Tenants.CancelTenant;

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
