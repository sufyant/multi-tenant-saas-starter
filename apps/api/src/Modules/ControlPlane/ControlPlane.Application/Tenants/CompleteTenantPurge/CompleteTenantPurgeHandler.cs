using ControlPlane.Application.Users.DeleteUserWithoutTenant;
using ControlPlane.Contracts.Tenants;
using JasperFx;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.CompleteTenantPurge;

// The tenant purge's last step, after every module replied (docs/flows/tenant-purge.md). Under the tenant's lock (K6a), ControlPlane
// deletes its data of the tenant in the order the keys need and leaves the tombstone (ITenantRepository.PurgeAsync), in one transaction.
// It reads the Owners first, because the purge takes their memberships away and their notification needs them; an Owner without an
// address is a deleted user, who is told nothing. Then, after the commit, Audit writes tenant.purged in the platform's log, and the platform deletes each user the purge left without a
// tenant. Both go without a tenant: the tenant's own log is gone, and a user's deletion declares the user (R4). A tenant already purged
// only replies again (S7).
public static class CompleteTenantPurgeHandler
{
    private static readonly DeliveryOptions WithoutTenant = new() { TenantId = StorageConstants.DefaultTenantId };

    public static async Task<OutgoingMessages> HandleAsync(
        CompleteTenantPurgeCommand complete,
        ITenantRepository tenants,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("A tenant's purge runs in the tenant, whose row stays as its tombstone.");
        var now = time.GetUtcNow();
        if (tenant.Purge(now) == StateChange.Unchanged)
        {
            return [new CompleteTenantPurgeReply(complete.TenantId)];
        }

        var owners = (await tenants.FindOwnerAddressesAsync(cancellationToken)).Select(owner => owner.UserId).ToList();
        var withoutTenant = await tenants.PurgeAsync(tenant, cancellationToken);

        return
        [
            new TenantPurgedEvent(Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, owners).WithDeliveryOptions(WithoutTenant),
            .. withoutTenant.Select(user => new DeleteUserWithoutTenantCommand(user).WithDeliveryOptions(WithoutTenant)),
            new CompleteTenantPurgeReply(complete.TenantId),
        ];
    }
}
