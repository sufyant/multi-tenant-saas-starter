using Audit.Application.AuditLogs.ApplyTenantAuditRetention;
using Audit.Application.AuditLogs.DeleteExpiredPlatformEntries;
using ControlPlane.Contracts.Plans;
using Wolverine;

namespace Audit.Application.AuditLogs.ApplyAuditRetention;

// K14, K15: one message for each tenant that is not purging or purged, delivered in its tenant, since a transaction declares one tenant
// (R4), and one for the platform's log, which declares nobody. ControlPlane names the tenants in one query across them.
public static class ApplyAuditRetentionHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        ApplyAuditRetentionCommand command,
        IAuditRetentionDirectory periods,
        CancellationToken cancellationToken)
    {
        var tenants = await periods.ListAsync(cancellationToken);
        return
        [
            new DeleteExpiredPlatformEntriesCommand(),
            .. tenants.Select(tenant => new ApplyTenantAuditRetentionCommand(tenant.TenantId)
                .WithDeliveryOptions(new DeliveryOptions { TenantId = tenant.TenantId.ToString() })),
        ];
    }
}
