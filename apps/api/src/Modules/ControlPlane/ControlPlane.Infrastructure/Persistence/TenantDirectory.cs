using ControlPlane.Contracts;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence;

// Resolves a tenant route before any tenant is declared. The tenant must exist and be active in control_plane.tenants, which belongs to
// no tenant; the membership is then read in a transaction that declares that tenant first (R4), so row level security decides
// what it sees. The membership comes with its role's permissions, so the read stays one query.
// It runs in the HTTP pipeline, before any message is sent, so no handler's transaction is there to join. It is the only place
// outside a handler that begins a transaction of its own (W1).
internal sealed class TenantDirectory(ControlPlaneDbContext controlPlane) : ITenantDirectory
{
    public async Task<Result<TenantMembership>?> FindMembershipAsync(Guid tenantId, string externalUserId, CancellationToken cancellationToken)
    {
        if (!await controlPlane.Tenants.AnyAsync(tenant => tenant.Id == tenantId && tenant.Status == TenantStatus.Active, cancellationToken))
        {
            return null;
        }

        await using var transaction = await controlPlane.Database.BeginTransactionAsync(cancellationToken);
        await controlPlane.DeclareTenantAsync(tenantId, cancellationToken);
        var memberRole = await (
                from membership in controlPlane.Memberships
                join user in controlPlane.Users on membership.UserId equals user.Id
                join role in controlPlane.Roles on membership.RoleId equals role.Id
                where user.ExternalId == externalUserId
                select role)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return memberRole is null ? null : (Result<TenantMembership>)new TenantMembership(tenantId, memberRole.Permissions);
    }
}
