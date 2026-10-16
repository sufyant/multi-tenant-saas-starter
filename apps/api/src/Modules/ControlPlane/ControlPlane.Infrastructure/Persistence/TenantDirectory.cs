using ControlPlane.Application;
using ControlPlane.Contracts;
using ControlPlane.Domain;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence;

// Resolves a tenant route before any tenant is declared. The tenant must exist and be active or suspended in control_plane.tenants,
// which belongs to no tenant; the membership is then read in a transaction that declares that tenant first (R4), so row level security
// decides what it sees. The membership comes with its role's permissions, so the read stays one query. Only a member learns that the
// tenant is suspended (T3). Anyone else gets the handlers' own tenant.not_found, so the door's answer is the handlers' answer (T7).
// It runs in the HTTP pipeline, before any message is sent, so no handler's transaction is there to join. It is the only place
// outside a handler that begins a transaction of its own (W1).
internal sealed class TenantDirectory(ControlPlaneDbContext controlPlane) : ITenantDirectory
{
    private static readonly Error Suspended = Error.Forbidden(ControlPlaneErrorCodes.TenantSuspended, "The tenant is suspended.");

    public async Task<Result<TenantMembership>> FindMembershipAsync(Guid tenantId, string externalUserId, CancellationToken cancellationToken)
    {
        var status = await controlPlane.Tenants
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => (TenantStatus?)tenant.Status)
            .SingleOrDefaultAsync(cancellationToken);
        if (status is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            return Errors.TenantNotFound;
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

        if (memberRole is null)
        {
            return Errors.TenantNotFound;
        }

        return status == TenantStatus.Suspended ? Suspended : new TenantMembership(tenantId, memberRole.Permissions);
    }
}
