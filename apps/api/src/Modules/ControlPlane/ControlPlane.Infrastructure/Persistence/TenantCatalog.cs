using ControlPlane.Application.Ports;
using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence;

/// <summary>
/// The one way to reach catalog rows that belong to a tenant, used only inside a tenant. Row level security keeps one tenant's
/// memberships and invitations from another; this access point adds no filter of its own. The active tenant is the tenant of the
/// message the handler serves (W2).
/// </summary>
/// <remarks>Public only so that Wolverine's generated code can build it on the handler's own DbContext (W1, W9).</remarks>
public sealed class TenantCatalog(ControlPlaneDbContext controlPlane) : ITenantCatalog
{
    internal IQueryable<Membership> Memberships
    {
        get
        {
            _ = ActiveTenant; // Fails fast outside a tenant.
            return controlPlane.Memberships;
        }
    }

    Guid ITenantCatalog.TenantId => ActiveTenant;

    private Guid ActiveTenant =>
        controlPlane.MessageTenantId ?? throw new InvalidOperationException("Tenant-owned catalog rows are reached only inside a tenant.");

    Task<Tenant> ITenantCatalog.FindTenantAsync(CancellationToken cancellationToken)
    {
        var tenantId = ActiveTenant;
        return controlPlane.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId, cancellationToken);
    }

    async Task<Tenant?> ITenantCatalog.FindTenantForUpdateAsync(CancellationToken cancellationToken)
    {
        var found = await controlPlane.Tenants
            .FromSql($"SELECT * FROM control_plane.tenants WHERE id = {ActiveTenant} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    // The tenants table belongs to no tenant, so the list needs no active tenant and sees every tenant.
    async Task<ListPage<Tenant>> ITenantCatalog.ListTenantsAsync(PageRequest paging, CancellationToken cancellationToken)
    {
        var total = await controlPlane.Tenants.CountAsync(cancellationToken);
        var page = await controlPlane.Tenants
            .OrderBy(tenant => tenant.Name)
            .ThenBy(tenant => tenant.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new ListPage<Tenant>(page, paging, total);
    }

    Task<Membership?> ITenantCatalog.FindMembershipAsync(Guid userId, CancellationToken cancellationToken) =>
        Memberships.SingleOrDefaultAsync(membership => membership.UserId == userId, cancellationToken);

    // Row level security keeps the list to the active tenant; the query names no tenant.
    async Task<ListPage<(Guid UserId, BuiltInRole Role)>> ITenantCatalog.ListMembersAsync(PageRequest paging, CancellationToken cancellationToken)
    {
        var members =
            from membership in Memberships
            join role in controlPlane.Roles on membership.RoleId equals role.Id
            select new { membership.UserId, role.BuiltIn };

        var total = await members.CountAsync(cancellationToken);
        var page = await members
            .OrderBy(member => member.BuiltIn == BuiltInRole.Owner ? 0 : member.BuiltIn == BuiltInRole.Admin ? 1 : 2)
            .ThenBy(member => member.UserId)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new([.. page.Select(member => (member.UserId, member.BuiltIn))], paging, total);
    }

    async Task<Invitation?> ITenantCatalog.FindInvitationForUpdateAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Invitations
            .FromSql($"SELECT * FROM control_plane.invitations WHERE token_hash = {tokenHash} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    async Task<Invitation?> ITenantCatalog.FindInvitationForUpdateAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Invitations
            .FromSql($"SELECT * FROM control_plane.invitations WHERE id = {invitationId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    Task<User?> ITenantCatalog.FindUserAsync(string externalUserId, CancellationToken cancellationToken) =>
        controlPlane.Users.SingleOrDefaultAsync(user => user.ExternalId == externalUserId, cancellationToken);

    Task<bool> ITenantCatalog.IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.AnyAsync(admin => admin.UserId == userId, cancellationToken);

    public void AddMember(Guid userId, Guid roleId) => controlPlane.Memberships.Add(new Membership(ActiveTenant, userId, roleId));

    // An insert of a slug that another tenant creation has inserted but not yet committed waits for it, and is skipped if it commits.
    async Task<bool> ITenantCatalog.TryAddAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var added = OfActiveTenant(tenant, tenant.Id);
        var inserted = await controlPlane.Database.ExecuteSqlAsync(
            $"INSERT INTO control_plane.tenants (id, name, slug, status) VALUES ({added.Id}, {added.Name}, {added.Slug}, {added.Status.ToString()}) ON CONFLICT (slug) DO NOTHING",
            cancellationToken);

        return inserted == 1;
    }

    void ITenantCatalog.Add(Invitation invitation) => controlPlane.Invitations.Add(invitation);

    void ITenantCatalog.Add(User user) => controlPlane.Users.Add(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);

    // The tenants table belongs to no tenant, so row level security does not stop a tenant from adding another one.
    private T OfActiveTenant<T>(T row, Guid? tenantId) =>
        tenantId == ActiveTenant ? row : throw new InvalidOperationException("Only rows of the active tenant can be written here.");
}
