using ControlPlane.Application.Tenants.TenantCreation;
using Microsoft.Extensions.Logging;
using SharedKernel;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace ControlPlane.Application.Tenants.CancelTenant;

public static partial class CancelTenantHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().RetryTimes(3);

    // An active or suspended tenant is past the pivot and is never cancelled: the compensation changes nothing and reports nothing, and
    // the saga's cancellation timeout leaves the tenant creation to a person (Tenant.CancelCreation). A cancelled tenant keeps its first
    // reason.
    public static async Task<OutgoingMessages> HandleAsync(
        CancelTenantCommand step,
        ITenantRepository tenants,
        ILogger<TenantCreationSaga> logger,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("A tenant is cancelled after its creation added it.");
        if (tenant.CancelCreation(step.Reason) is not { } cancelled)
        {
            return [];
        }

        if (cancelled == StateChange.Changed)
        {
            LogCancelled(logger, step.TenantId, step.Reason);
        }

        (await tenants.FindInvitationForUpdateAsync(step.InvitationId, cancellationToken))?.Cancel();
        await tenants.SaveAsync(tenant, cancellationToken);
        return [new CancelTenantReply(step.TenantId)];
    }

    [LoggerMessage(EventName = "TenantCancelled", Level = LogLevel.Warning, Message = "Tenant {TenantId} is cancelled: {Reason}")]
    private static partial void LogCancelled(ILogger logger, Guid tenantId, string reason);
}
