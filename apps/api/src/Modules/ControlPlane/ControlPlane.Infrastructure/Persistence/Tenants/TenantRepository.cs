using ControlPlane.Application.Tenants;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

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
        summary.Summarize(members, owners, latestOwnerInvitation);
        await controlPlane.SaveChangesAsync(cancellationToken);
        controlPlane.SummaryWritten();
    }

    Task<Membership?> ITenantRepository.FindMembershipAsync(Guid userId, CancellationToken cancellationToken) =>
        Memberships.SingleOrDefaultAsync(membership => membership.UserId == userId, cancellationToken);

    async Task<MemberFacts> ITenantRepository.FindMemberFactsAsync(Guid userId, CancellationToken cancellationToken) =>
        new(await ((ITenantRepository)this).FindMembershipAsync(userId, cancellationToken), await CountOwnersAsync(cancellationToken));

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
            await ((ITenantRepository)this).FindPendingInvitationsAsync(cancellationToken));
    }

    // Row level security keeps the invitations to the active tenant; the query names no tenant.
    async Task<IReadOnlyList<Invitation>> ITenantRepository.FindPendingInvitationsAsync(CancellationToken cancellationToken) =>
        await controlPlane.TenantInvitations.Where(invitation => invitation.Status == InvitationStatus.Pending).ToListAsync(cancellationToken);

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
    // The tenant's summary is added with it, empty, so every tenant has one (R12).
    async Task<bool> ITenantRepository.TryAddAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var added = OfActiveTenant(tenant, tenant.Id);
        var inserted = await controlPlane.Database.ExecuteSqlAsync(
            $"""
            WITH tenant AS (
                INSERT INTO control_plane.tenants (id, name, slug, status, created_at)
                VALUES ({added.Id}, {added.Name}, {added.Slug}, {added.Status.ToString()}, {added.CreatedAt}) ON CONFLICT (slug) DO NOTHING
                RETURNING id)
            INSERT INTO control_plane.tenant_summaries (tenant_id) SELECT id FROM tenant
            """,
            cancellationToken);

        return inserted == 1;
    }

    void ITenantRepository.Add(Invitation invitation) => controlPlane.Invitations.Add(invitation);

    async Task ITenantRepository.AddInvitationAsync(Tenant tenant, Invited invited, CancellationToken cancellationToken)
    {
        if (invited.Replaced.Count > 0)
        {
            await controlPlane.SaveChangesAsync(cancellationToken);
        }

        controlPlane.Invitations.Add(invited.Invitation);
        await ((ITenantRepository)this).SaveAsync(tenant, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);

    // Row level security keeps the count to the active tenant. It joins no user, so a deleted user's Owner membership counts too.
    private Task<int> CountOwnersAsync(CancellationToken cancellationToken) =>
        Memberships.CountAsync(membership => membership.RoleId == BuiltInRoles.Owner.Id, cancellationToken);

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

    // Row level security keeps every count to the active tenant. The latest Owner invitation is read only when the tenant has no Owner,
    // which is the only time the summary keeps it.
    private async Task<(int Members, int Owners, Invitation? LatestOwnerInvitation)> ReadSummaryAsync(CancellationToken cancellationToken)
    {
        var owners = await Memberships.CountAsync(membership => membership.RoleId == BuiltInRoles.Owner.Id, cancellationToken);

        return (
            await Memberships.CountAsync(cancellationToken),
            owners,
            owners == 0
                ? await controlPlane.TenantInvitations
                    .Where(invitation => invitation.RoleId == BuiltInRoles.Owner.Id)
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
