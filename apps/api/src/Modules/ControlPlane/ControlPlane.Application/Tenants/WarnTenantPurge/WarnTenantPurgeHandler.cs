using System.Security.Cryptography;
using System.Text;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Domain.Tenants;
using Wolverine;

namespace ControlPlane.Application.Tenants.WarnTenantPurge;

// The warning of a closed tenant's purge. It locks the tenant row (K6a), so a reopening runs before or after it, and acts only for a tenant
// still closed at the message's closure: a reopening cancels no scheduled message, so the message itself checks (K4). It writes nothing
// in ControlPlane: it hands each Owner's email to Notifications and the warning to Audit. A warning of one closure has one id and one
// moment, the moment it was scheduled for, so a copy that arrives again later records one entry (R18), and each email's idempotency key
// is the Owner's warning of this purge (O4).
public static class WarnTenantPurgeHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        WarnTenantPurgeCommand warn,
        ITenantRepository tenants,
        TenantOptions settings,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant || !tenant.IsClosedAt(warn.ClosedAt))
        {
            return [];
        }

        var now = time.GetUtcNow();
        var purgesAt = warn.ClosedAt + settings.ReopenPeriod;
        var owners = await tenants.FindOwnerAddressesAsync(cancellationToken);
        return
        [
            new TenantPurgeWarnedEvent(
                WarningId(tenant.Id, warn.ClosedAt),
                Tenant.PurgeWarningMomentOf(warn.ClosedAt, settings.ReopenPeriod, settings.PurgeWarningLead),
                tenant.Id,
                tenant.Name,
                purgesAt),
            .. owners.Select(owner =>
                new TenantPurgeWarningReadyEvent(
                    Guid.CreateVersion7(now), now, tenant.Id, owner.UserId, owner.Email, tenant.Name, owner.Language, purgesAt, owner.TimeZone)),
        ];
    }

    // The same tenant and closure always give the same id.
    private static Guid WarningId(Guid tenantId, DateTimeOffset closedAt) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"tenant-purge-warned/{tenantId}/{closedAt.UtcTicks}"))[..16]);
}
