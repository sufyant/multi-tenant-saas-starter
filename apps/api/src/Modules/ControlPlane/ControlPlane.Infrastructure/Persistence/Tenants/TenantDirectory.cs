using ControlPlane.Application;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Tenancy.Caching;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

// Resolves a tenant route before any tenant is declared. The tenant must exist, in any status, in
// control_plane.tenants, which belongs to no tenant; the membership is then read in a transaction that declares that tenant first (R4),
// so row level security decides what it sees. The membership comes with its role's permissions, a custom role's rows included (A3), so
// the read stays one query. Only a
// member learns that the tenant is suspended, closed or purging (T3), from the tenant's own rule, which every tenant command asks again under the
// tenant's lock (K6g). Anyone else gets the handlers' own tenant.not_found, so the door's answer is the handlers' answer (T7). The cache
// keeps what the rows say, the tenant's status and the member's role, for a minute at most (C4, C8), and the rule reads it on every
// request.
// It runs in the HTTP pipeline, before any message is sent, so no handler's transaction is there to join. It is the only place
// outside a handler that begins a transaction of its own (W1).
internal sealed class TenantDirectory(ControlPlaneDbContext controlPlane, ReadCache cache) : ITenantDirectory
{
    public async Task<Result<TenantMembership>> FindMembershipAsync(Guid tenantId, string externalUserId, CancellationToken cancellationToken)
    {
        var found = await cache.ReadAsync(
            CachedReads.MemberAccess, tenantId, externalUserId, token => ReadAsync(tenantId, externalUserId, token), cancellationToken);
        if (found is not { Status: { } status, Member: true })
        {
            return Errors.TenantNotFound;
        }

        var access = Tenant.CheckMemberAccess(status);
        IReadOnlySet<string> permissions = found.BuiltIn is { } builtIn ? BuiltInRoles.PermissionsOf(builtIn) : found.Grants.ToHashSet(StringComparer.Ordinal);
        return access.IsSuccess ? new TenantMembership(tenantId, permissions) : access.Error;
    }

    private async Task<MemberAccess> ReadAsync(Guid tenantId, string externalUserId, CancellationToken cancellationToken)
    {
        var status = await controlPlane.Tenants
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => (TenantStatus?)tenant.Status)
            .SingleOrDefaultAsync(cancellationToken);
        if (status is null)
        {
            return new MemberAccess(null, Member: false, null, []);
        }

        await using var transaction = await controlPlane.Database.BeginTransactionAsync(cancellationToken);
        await controlPlane.DeclareTenantAsync(tenantId, cancellationToken);
        var memberRole = await (
                from membership in controlPlane.Memberships
                join user in controlPlane.Users on membership.UserId equals user.Id
                join role in controlPlane.Roles on membership.RoleId equals role.Id
                where user.ExternalId == externalUserId
                select role)
            .Include(role => role.Grants)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return memberRole is null
            ? new MemberAccess(status, Member: false, null, [])
            : new MemberAccess(status, Member: true, memberRole.BuiltIn, [.. memberRole.Grants.Select(grant => grant.Permission).Order(StringComparer.Ordinal)]);
    }
}
