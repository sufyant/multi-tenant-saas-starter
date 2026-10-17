using ControlPlane.Application.Invitations;
using ControlPlane.Application.Members;
using ControlPlane.Application.Ports;
using ControlPlane.Application.Tenants;
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

    private IQueryable<Invitation> Invitations
    {
        get
        {
            _ = ActiveTenant; // Fails fast outside a tenant.
            return controlPlane.Invitations;
        }
    }

    Guid ITenantCatalog.TenantId => ActiveTenant;

    private Guid ActiveTenant =>
        controlPlane.MessageTenantId ?? throw new InvalidOperationException("Tenant-owned catalog rows are reached only inside a tenant.");

    async Task<Tenant?> ITenantCatalog.FindTenantForUpdateAsync(CancellationToken cancellationToken)
    {
        var found = await controlPlane.Tenants
            .FromSql($"SELECT * FROM control_plane.tenants WHERE id = {ActiveTenant} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    Task<Tenant?> ITenantCatalog.FindTenantAsync(CancellationToken cancellationToken) =>
        controlPlane.Tenants.AsNoTracking().SingleOrDefaultAsync(tenant => tenant.Id == ActiveTenant, cancellationToken);

    // The tenants table belongs to no tenant, so the list needs no active tenant and sees every tenant.
    async Task<ListPage<Tenant>> ITenantCatalog.ListTenantsAsync(ListRequest<ListTenantsSort> list, TenantStatus? status, CancellationToken cancellationToken)
    {
        var tenants = controlPlane.Tenants.AsQueryable();
        if (status is { } wanted)
        {
            tenants = tenants.Where(tenant => tenant.Status == wanted);
        }

        if (list.Search is { } search)
        {
            var pattern = ListQueries.ContainsPattern(search);
            tenants = tenants.Where(tenant =>
                EF.Functions.ILike(tenant.Name, pattern, ListQueries.Escape) || EF.Functions.ILike(tenant.Slug, pattern, ListQueries.Escape));
        }

        var total = await tenants.CountAsync(cancellationToken);
        var sorted = list.Sort switch
        {
            ListTenantsSort.Slug => tenants.OrderBy(tenant => tenant.Slug, list.Descending),
            _ => tenants.OrderBy(tenant => tenant.Name, list.Descending),
        };
        var page = await sorted
            .ThenBy(tenant => tenant.Id)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new ListPage<Tenant>(page, list.Paging, total);
    }

    Task<Membership?> ITenantCatalog.FindMembershipAsync(Guid userId, CancellationToken cancellationToken) =>
        Memberships.SingleOrDefaultAsync(membership => membership.UserId == userId, cancellationToken);

    // Row level security keeps the count to the active tenant. It joins no user, so a deleted user's Owner membership counts too.
    Task<int> ITenantCatalog.CountOwnersAsync(CancellationToken cancellationToken) =>
        Memberships.CountAsync(membership => membership.RoleId == BuiltInRoles.Owner.Id, cancellationToken);

    // Row level security keeps the list to the active tenant; the query names no tenant. The users table is not under row level security
    // (R6), so the query starts from the memberships and joins each member's user: a user of another tenant never enters it.
    async Task<ListPage<(Guid UserId, string? Email, string? FirstName, string? LastName, BuiltInRole Role, bool Deleted)>> ITenantCatalog.ListMembersAsync(
        ListRequest<ListMembersSort> list,
        BuiltInRole? role,
        CancellationToken cancellationToken)
    {
        var members =
            from membership in Memberships
            join memberRole in controlPlane.Roles on membership.RoleId equals memberRole.Id
            join user in controlPlane.Users on membership.UserId equals user.Id
            select new { membership.UserId, user.Email, user.FirstName, user.LastName, memberRole.BuiltIn, Deleted = user.DeletedAt != null };
        if (role is { } wanted)
        {
            members = members.Where(member => member.BuiltIn == wanted);
        }

        if (list.Search is { } search)
        {
            var pattern = ListQueries.ContainsPattern(search);
            members = members.Where(member =>
                EF.Functions.ILike(member.Email!, pattern, ListQueries.Escape)
                || EF.Functions.ILike(member.FirstName!, pattern, ListQueries.Escape)
                || EF.Functions.ILike(member.LastName!, pattern, ListQueries.Escape));
        }

        // A member without an email address or a last name, such as a deleted user, comes last in both directions.
        var total = await members.CountAsync(cancellationToken);
        var sorted = list.Sort switch
        {
            ListMembersSort.Email => members.OrderBy(member => member.Email == null).ThenBy(member => member.Email, list.Descending),
            ListMembersSort.LastName => members.OrderBy(member => member.LastName == null).ThenBy(member => member.LastName, list.Descending),
            _ => members.OrderBy(
                member => member.BuiltIn == BuiltInRole.Owner ? 0 : member.BuiltIn == BuiltInRole.Admin ? 1 : 2,
                list.Descending),
        };
        var page = await sorted
            .ThenBy(member => member.UserId)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .ToListAsync(cancellationToken);

        return new([.. page.Select(member => (member.UserId, member.Email, member.FirstName, member.LastName, member.BuiltIn, member.Deleted))], list.Paging, total);
    }

    // R6: the users table is not under row level security, so the query starts from the active tenant's memberships, which row level
    // security keeps to the tenant. lower() on both sides compares the address without case, in the database's own rules.
    Task<bool> ITenantCatalog.IsMemberEmailAsync(string email, CancellationToken cancellationToken)
    {
        _ = ActiveTenant; // Fails fast outside a tenant.
        return controlPlane.Database.SqlQuery<int>(
                $"""
                SELECT 1 AS "Value" FROM control_plane.memberships
                JOIN control_plane.users ON users.id = memberships.user_id
                WHERE lower(users.email) = lower({email})
                """)
            .AnyAsync(cancellationToken);
    }

    Task<int> ITenantCatalog.CountInvitationsSinceAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
        Invitations.CountAsync(invitation => invitation.CreatedAt > since, cancellationToken);

    // Row level security keeps the invitations to the active tenant; the query names no tenant.
    async Task<IReadOnlyList<Invitation>> ITenantCatalog.FindPendingInvitationsAsync(CancellationToken cancellationToken) =>
        await Invitations.Where(invitation => invitation.Status == InvitationStatus.Pending).ToListAsync(cancellationToken);

    // The same comparison as the partial unique index, which serves the query (K2): lower(email), pending only. Row level security
    // adds the tenant.
    async Task<Invitation?> ITenantCatalog.FindPendingInvitationAsync(string email, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Invitations
            .FromSql($"SELECT * FROM control_plane.invitations WHERE lower(email) = lower({email}) AND status = 'Pending'")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    // Row level security keeps the list to the active tenant; the query names no tenant. Two pending invitations never share an address,
    // so only the expiry can tie, and the id breaks every tie (K5).
    async Task<ListPage<Invitation>> ITenantCatalog.ListPendingInvitationsAsync(
        ListRequest<ListInvitationsSort> list,
        BuiltInRole? role,
        CancellationToken cancellationToken)
    {
        var invitations = Invitations.Where(invitation => invitation.Status == InvitationStatus.Pending);
        if (role is { } wanted)
        {
            var roleId = BuiltInRoles.All.Single(builtIn => builtIn.BuiltIn == wanted).Id;
            invitations = invitations.Where(invitation => invitation.RoleId == roleId);
        }

        if (list.Search is { } search)
        {
            var pattern = ListQueries.ContainsPattern(search);
            invitations = invitations.Where(invitation => EF.Functions.ILike(invitation.Email, pattern, ListQueries.Escape));
        }

        var total = await invitations.CountAsync(cancellationToken);
        var sorted = list.Sort switch
        {
            ListInvitationsSort.Email => invitations.OrderBy(invitation => invitation.Email, list.Descending),
            _ => invitations.OrderBy(invitation => invitation.ExpiresAt, list.Descending),
        };
        var page = await sorted
            .ThenBy(invitation => invitation.Id)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new ListPage<Invitation>(page, list.Paging, total);
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

    async Task<User?> ITenantCatalog.FindUserForShareAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Users
            .FromSql($"SELECT * FROM control_plane.users WHERE external_id = {externalUserId} FOR SHARE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    Task<User?> ITenantCatalog.FindUserAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    Task<bool> ITenantCatalog.IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.AnyAsync(admin => admin.UserId == userId, cancellationToken);

    public void AddMember(Guid userId, Guid roleId) => controlPlane.Memberships.Add(new Membership(ActiveTenant, userId, roleId));

    void ITenantCatalog.Remove(Membership membership) => controlPlane.Memberships.Remove(OfActiveTenant(membership, membership.TenantId));

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
