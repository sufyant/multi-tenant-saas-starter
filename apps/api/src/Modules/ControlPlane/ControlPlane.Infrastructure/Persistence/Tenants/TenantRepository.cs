using ControlPlane.Application.Invitations;
using ControlPlane.Application.Tenants;
using ControlPlane.Domain;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Infrastructure.Persistence.Roles;
using Microsoft.EntityFrameworkCore;
using Tenancy.Caching;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

/// <summary>
/// The tenant aggregate for the commands, inside the active tenant, which is the tenant of the message the handler serves (W2). Row
/// level security keeps one tenant's memberships and invitations from another; this repository adds no filter of its own.
/// </summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class TenantRepository(ControlPlaneDbContext controlPlane) : ITenantRepository
{
    Guid ITenantRepository.TenantId => controlPlane.ActiveTenant;

    internal IQueryable<Membership> Memberships => controlPlane.TenantMemberships;

    async Task<Tenant?> ITenantRepository.FindTenantForUpdateAsync(CancellationToken cancellationToken)
    {
        var found = await controlPlane.Tenants
            .FromSql($"SELECT * FROM control_plane.tenants WHERE id = {controlPlane.ActiveTenant} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    // The changes are saved first, so the summary is read from the rows as the transaction leaves them, and then saved too. The
    // DbContext refuses to commit a change to a membership or an invitation that no summary followed (TenantSummaryInterceptor).
    async Task ITenantRepository.SaveAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var tenantId = OfActiveTenant(tenant, tenant.Id).Id;
        await controlPlane.SaveChangesAsync(cancellationToken);
        var summary = await controlPlane.TenantSummaries.SingleAsync(found => found.TenantId == tenantId, cancellationToken);
        var (members, owners, latestOwnerInvitation) = await ReadSummaryAsync(cancellationToken);
        var customRoles = await controlPlane.TenantRoles.CountAsync(role => role.BuiltIn == null, cancellationToken);
        var pendingExpiries = await controlPlane.TenantInvitations
            .Where(invitation => invitation.Status == InvitationStatus.Pending)
            .Select(invitation => invitation.ExpiresAt)
            .ToListAsync(cancellationToken);
        summary.Summarize(members, owners, latestOwnerInvitation, customRoles, pendingExpiries);
        await controlPlane.SaveChangesAsync(cancellationToken);
        controlPlane.SummaryWritten();
    }

    Task<Membership?> ITenantRepository.FindMembershipAsync(Guid userId, CancellationToken cancellationToken) =>
        Memberships.SingleOrDefaultAsync(membership => membership.UserId == userId, cancellationToken);

    // Row level security keeps the roles and their permissions to the active tenant; the query names no tenant.
    async Task<TenantRoles> ITenantRepository.FindRolesAsync(CancellationToken cancellationToken) =>
        new(await controlPlane.TenantRoles.Include(role => role.Grants).AsNoTracking().ToListAsync(cancellationToken));

    // Row level security keeps every read to the active tenant. A name is compared with lower(), as the unique index on the tenant and
    // lower(name) compares it, so the check and the index never disagree.
    async Task<RoleFacts> ITenantRepository.FindRoleFactsAsync(Guid? roleId, string? name, CancellationToken cancellationToken)
    {
        var target = roleId is { } id
            ? await controlPlane.TenantRoles.Include(role => role.Grants).SingleOrDefaultAsync(role => role.Id == id, cancellationToken)
            : null;
        var nameTaken = name is not null
            && await controlPlane.Database.SqlQuery<int>(
                    $"""
                    SELECT 1 AS "Value" FROM control_plane.roles
                    WHERE lower(name) = lower({name}) AND id IS DISTINCT FROM {roleId}
                    """)
                .AnyAsync(cancellationToken);
        var use = target is not null ? (await controlPlane.OfAsync([target.Id], cancellationToken))[target.Id] : RoleUse.None;
        return new RoleFacts(target, await controlPlane.TenantRoles.CountAsync(role => role.BuiltIn == null, cancellationToken), nameTaken, use);
    }

    async Task ITenantRepository.AddRoleAsync(Tenant tenant, Role role, CancellationToken cancellationToken)
    {
        controlPlane.Roles.Add(OfActiveTenant(role, role.TenantId));
        await ((ITenantRepository)this).SaveAsync(tenant, cancellationToken);
    }

    // A permission's row has the tenant column, which the database fills (TenantDbContext), so EF Core takes a new row it finds in the
    // role's permissions for one that exists. The rows of the added permissions are added here by name; a removed row is an orphan of
    // its role, which EF Core deletes.
    async Task ITenantRepository.SaveRoleAsync(RoleUpdate update, CancellationToken cancellationToken)
    {
        var role = OfActiveTenant(update.Role, update.Role.TenantId);
        controlPlane.ChangeTracker.DetectChanges();
        foreach (var grant in role.Grants.Where(grant => update.Added.Contains(grant.Permission)))
        {
            controlPlane.Entry(grant).State = EntityState.Added;
        }

        await controlPlane.SaveChangesAsync(cancellationToken);
    }

    async Task ITenantRepository.DeleteRoleAsync(Tenant tenant, Role role, CancellationToken cancellationToken)
    {
        controlPlane.Roles.Remove(OfActiveTenant(role, role.TenantId));
        await ((ITenantRepository)this).SaveAsync(tenant, cancellationToken);
    }

    async Task<MemberFacts> ITenantRepository.FindMemberFactsAsync(Guid userId, CancellationToken cancellationToken) =>
        new(
            await ((ITenantRepository)this).FindMembershipAsync(userId, cancellationToken),
            await CountOwnersAsync(cancellationToken),
            await ((ITenantRepository)this).FindRolesAsync(cancellationToken));

    // The plans, their values and the overrides belong to no tenant (R6); the tenant row is the one the caller locked.
    async Task<EffectiveEntitlements> ITenantRepository.FindEntitlementsAsync(Tenant tenant, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var locked = OfActiveTenant(tenant, tenant.Id);
        var plan = await controlPlane.PlanEntitlements.Where(value => value.PlanId == locked.PlanId).AsNoTracking().ToListAsync(cancellationToken);
        var overrides = await controlPlane.EntitlementOverrides.Where(entry => entry.TenantId == locked.Id).AsNoTracking().ToListAsync(cancellationToken);
        return EffectiveEntitlements.Of(
            plan.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal), overrides.Select(entry => entry.ToValue()), now);
    }

    // An override belongs to no tenant's row level security (R6); the key names the active tenant's.
    Task<EntitlementOverride?> ITenantRepository.FindOverrideAsync(string key, CancellationToken cancellationToken) =>
        controlPlane.EntitlementOverrides.SingleOrDefaultAsync(entry => entry.TenantId == controlPlane.ActiveTenant && entry.Key == key, cancellationToken);

    void ITenantRepository.AddOverride(EntitlementOverride entry) => controlPlane.EntitlementOverrides.Add(OfActiveTenant(entry, entry.TenantId));

    void ITenantRepository.RemoveOverride(EntitlementOverride entry) => controlPlane.EntitlementOverrides.Remove(OfActiveTenant(entry, entry.TenantId));

    Task<int> ITenantRepository.CountInvitationsSinceAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
        controlPlane.TenantInvitations.CountAsync(invitation => invitation.CreatedAt > since, cancellationToken);

    // Every pending invitation comes, expired ones included; Tenant.Invite decides which of them the new one replaces. Row level
    // security adds the tenant to every query.
    async Task<InvitationFacts> ITenantRepository.FindInvitationFactsAsync(EmailAddress email, DateTimeOffset since, CancellationToken cancellationToken)
    {
        return new InvitationFacts(
            await CountOwnersAsync(cancellationToken),
            await IsMemberEmailAsync(email, cancellationToken),
            await ((ITenantRepository)this).CountInvitationsSinceAsync(since, cancellationToken),
            await ((ITenantRepository)this).FindPendingInvitationsAsync(cancellationToken),
            await Memberships.CountAsync(cancellationToken));
    }

    // Row level security keeps the invitations to the active tenant; the query names no tenant.
    async Task<IReadOnlyList<Invitation>> ITenantRepository.FindPendingInvitationsAsync(CancellationToken cancellationToken) =>
        await controlPlane.TenantInvitations.Where(invitation => invitation.Status == InvitationStatus.Pending).ToListAsync(cancellationToken);

    // Row level security keeps the invitations to the active tenant; the query names no tenant. Every address is in its one form, so
    // plain equality compares them.
    async Task<IReadOnlyList<Invitation>> ITenantRepository.FindInvitationsOfUserAsync(Guid userId, string? email, CancellationToken cancellationToken)
    {
        var accepted = controlPlane.TenantInvitations.Where(invitation => invitation.AcceptedBy == userId).Select(invitation => invitation.Email);
        return await controlPlane.TenantInvitations
            .Where(invitation => invitation.AcceptedBy == userId || (email != null && invitation.Email == email) || accepted.Contains(invitation.Email))
            .ToListAsync(cancellationToken);
    }

    async Task<Invitation?> ITenantRepository.FindInvitationForUpdateAsync(string secretHash, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Invitations
            .FromSql($"SELECT * FROM control_plane.invitations WHERE secret_hash = {secretHash} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    async Task<Invitation?> ITenantRepository.FindInvitationForUpdateAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Invitations
            .FromSql($"SELECT * FROM control_plane.invitations WHERE id = {invitationId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    // The one sequence of a membership's change (R13): the membership, the save with the tenant's summary, and the user's count.
    async Task ITenantRepository.AddMembershipAsync(Tenant tenant, Guid userId, Guid roleId, DateTimeOffset joinedAt, CancellationToken cancellationToken)
    {
        controlPlane.Memberships.Add(new Membership(controlPlane.ActiveTenant, userId, roleId, joinedAt));
        await FollowMembershipAsync(tenant, userId, 1, cancellationToken);
    }

    async Task ITenantRepository.RemoveMembershipAsync(Tenant tenant, Membership membership, CancellationToken cancellationToken)
    {
        controlPlane.Memberships.Remove(OfActiveTenant(membership, membership.TenantId));
        await FollowMembershipAsync(tenant, membership.UserId, -1, cancellationToken);
    }

    // An insert of a slug that another tenant creation has inserted but not yet committed waits for it, and is skipped if it commits.
    // The tenant's summary is added with it, empty, so every tenant has one (R12). Its roles follow only when the tenant was added; row
    // level security keeps them to the active tenant, which is the new one.
    async Task<bool> ITenantRepository.TryAddAsync(Tenant tenant, IReadOnlyList<Role> roles, CancellationToken cancellationToken)
    {
        var added = OfActiveTenant(tenant, tenant.Id);
        var inserted = await controlPlane.Database.ExecuteSqlAsync(
            $"""
            WITH tenant AS (
                INSERT INTO control_plane.tenants (id, name, slug, status, plan_id, created_at)
                VALUES ({added.Id}, {added.Name}, {added.Slug}, {added.Status.ToString()}, {added.PlanId}, {added.CreatedAt}) ON CONFLICT (slug) DO NOTHING
                RETURNING id)
            INSERT INTO control_plane.tenant_summaries (tenant_id) SELECT id FROM tenant
            """,
            cancellationToken);
        if (inserted != 1)
        {
            return false;
        }

        // The insert is raw SQL, so the change tracker does not see the tenant (C7).
        controlPlane.RemoveCacheTagsAfterCommit(CacheTag.Of(CacheTags.Tenant, added.Id));
        controlPlane.Roles.AddRange(roles.Select(role => OfActiveTenant(role, role.TenantId)));
        await controlPlane.SaveChangesAsync(cancellationToken);
        return true;
    }

    async Task ITenantRepository.AddInvitationAsync(Tenant tenant, Invited invited, CancellationToken cancellationToken)
    {
        if (invited.Replaced.Count > 0)
        {
            await controlPlane.SaveChangesAsync(cancellationToken);
        }

        controlPlane.Invitations.Add(invited.Invitation);
        await ((ITenantRepository)this).SaveAsync(tenant, cancellationToken);
    }

    // Row level security keeps the memberships and the roles to the active tenant; the users table is not under it (R6), so the query starts
    // from the memberships. An Owner is a member whose role holds the Owners' permission (A1).
    async Task<IReadOnlyList<OwnerAddress>> ITenantRepository.FindOwnerAddressesAsync(CancellationToken cancellationToken) =>
        await (
                from membership in Memberships
                join role in controlPlane.Roles.Where(Role.Holding(PermissionPools.TenantOwnersManage)) on membership.RoleId equals role.Id
                join user in controlPlane.Users on membership.UserId equals user.Id
                where user.Email != null
                orderby user.Id
                select new OwnerAddress(user.Id, user.Email!, user.Language, user.TimeZone))
            .ToListAsync(cancellationToken);

    // Users and system admins are under no row level security (R6), so the tenant's transaction reads them by the user's id.
    async Task<InvitingUser> ITenantRepository.FindInvitingUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await controlPlane.Users
            .Where(user => user.Id == userId)
            .Select(user => new InvitingUser(
                controlPlane.SystemAdmins.Any(admin => admin.UserId == user.Id), user.FirstName, user.LastName, user.TimeZone))
            .SingleAsync(cancellationToken);

    // The rows go by bulk statements, which row level security keeps to the active tenant, in the order the keys need: an invitation and a
    // membership name a role (A2), a role's permissions go with it (A3), and the summary and the overrides name the tenant (R12, E2). The summary goes with the
    // tenant's data, so no change of a membership or an invitation owes one (K6k). Each user's count changes in the order of the user ids,
    // so two purges that share users take their rows in one order. The change tracker does not see a bulk statement, so the purge removes
    // the tenant's cache tag itself, after the commit (C7), which the overrides' entries name too; the users' counts are in no cached answer.
    async Task<IReadOnlyList<Guid>> ITenantRepository.PurgeAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var purged = OfActiveTenant(tenant, tenant.Id);
        controlPlane.RemoveCacheTagsAfterCommit(CacheTag.Of(CacheTags.Tenant, purged.Id));
        var members = await Memberships.Select(membership => membership.UserId).Order().ToListAsync(cancellationToken);

        await controlPlane.TenantInvitations.ExecuteDeleteAsync(cancellationToken);
        await Memberships.ExecuteDeleteAsync(cancellationToken);
        foreach (var userId in members)
        {
            await controlPlane.Database.ExecuteSqlAsync(
                $"UPDATE control_plane.users SET membership_count = membership_count - 1 WHERE id = {userId}",
                cancellationToken);
        }

        await controlPlane.TenantRolePermissions.ExecuteDeleteAsync(cancellationToken);
        await controlPlane.TenantRoles.ExecuteDeleteAsync(cancellationToken);
        await controlPlane.TenantSummaries.Where(summary => summary.TenantId == purged.Id).ExecuteDeleteAsync(cancellationToken);
        await controlPlane.EntitlementOverrides.Where(entry => entry.TenantId == purged.Id).ExecuteDeleteAsync(cancellationToken);
        await controlPlane.SaveChangesAsync(cancellationToken);

        return await controlPlane.Users
            .Where(user => members.Contains(user.Id) && user.MembershipCount == 0 && user.DeletedAt == null)
            .Where(user => !controlPlane.SystemAdmins.Any(admin => admin.UserId == user.Id))
            .Select(user => user.Id)
            .Order()
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);

    // The members whose role holds the Owners' permission (A1, K1). Row level security keeps the count to the active tenant. It joins no
    // user, so a deleted user's Owner membership counts too.
    private Task<int> CountOwnersAsync(CancellationToken cancellationToken) =>
        Memberships
            .Join(controlPlane.Roles.Where(Role.Holding(PermissionPools.TenantOwnersManage)), membership => membership.RoleId, role => role.Id, (membership, _) => membership)
            .CountAsync(cancellationToken);

    // The count is changed in the database after the save, so a user the transaction adds has its row. The database adds the change to
    // the stored count, so the statement reads and writes the row under its own lock: two transactions of two tenants that count for one
    // user run one after the other, and neither loses the other's change.
    private async Task FollowMembershipAsync(Tenant tenant, Guid userId, int change, CancellationToken cancellationToken)
    {
        await ((ITenantRepository)this).SaveAsync(tenant, cancellationToken);
        await controlPlane.Database.ExecuteSqlAsync(
            $"UPDATE control_plane.users SET membership_count = membership_count + {change} WHERE id = {userId}",
            cancellationToken);
    }

    // R6: the users table is not under row level security, so the query starts from the active tenant's memberships, which row level
    // security keeps to the tenant. Both addresses are in their one form, so plain equality compares them.
    private Task<bool> IsMemberEmailAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        _ = controlPlane.ActiveTenant;
        return controlPlane.Database.SqlQuery<int>(
                $"""
                SELECT 1 AS "Value" FROM control_plane.memberships
                JOIN control_plane.users ON users.id = memberships.user_id
                WHERE users.email = {email.Value}
                """)
            .AnyAsync(cancellationToken);
    }

    // Row level security keeps every count to the active tenant. An Owner is a member whose role holds the Owners' permission, and an
    // Owner invitation gives such a role (A1, K1). The latest Owner invitation is read only when the tenant has no Owner, which is the only
    // time the summary keeps it.
    private async Task<(int Members, int Owners, Invitation? LatestOwnerInvitation)> ReadSummaryAsync(CancellationToken cancellationToken)
    {
        var owners = await CountOwnersAsync(cancellationToken);

        return (
            await Memberships.CountAsync(cancellationToken),
            owners,
            owners == 0
                ? await controlPlane.TenantInvitations
                    .Join(controlPlane.Roles.Where(Role.Holding(PermissionPools.TenantOwnersManage)), invitation => invitation.RoleId, role => role.Id, (invitation, _) => invitation)
                    .OrderByDescending(invitation => invitation.CreatedAt)
                    .ThenByDescending(invitation => invitation.Id)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellationToken)
                : null);
    }

    // The tenants table belongs to no tenant, so row level security does not stop a tenant from adding another one.
    private T OfActiveTenant<T>(T row, Guid? tenantId) =>
        tenantId == controlPlane.ActiveTenant ? row : throw new InvalidOperationException("Only rows of the active tenant can be written here.");
}
