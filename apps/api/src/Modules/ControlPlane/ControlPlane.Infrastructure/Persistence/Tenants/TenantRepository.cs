using ControlPlane.Application.Tenants;
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
        var locked = OfActiveTenant(tenant, tenant.Id);
        await controlPlane.SaveChangesAsync(cancellationToken);
        locked.Summarize(await ReadSummaryAsync(cancellationToken));
        await controlPlane.SaveChangesAsync(cancellationToken);
        controlPlane.SummaryWritten();
    }

    Task<Membership?> ITenantRepository.FindMembershipAsync(Guid userId, CancellationToken cancellationToken) =>
        Memberships.SingleOrDefaultAsync(membership => membership.UserId == userId, cancellationToken);

    // Row level security keeps the count to the active tenant. It joins no user, so a deleted user's Owner membership counts too.
    Task<int> ITenantRepository.CountOwnersAsync(CancellationToken cancellationToken) =>
        Memberships.CountAsync(membership => membership.RoleId == BuiltInRoles.Owner.Id, cancellationToken);

    // R6: the users table is not under row level security, so the query starts from the active tenant's memberships, which row level
    // security keeps to the tenant. lower() on both sides compares the address without case, in the database's own rules.
    Task<bool> ITenantRepository.IsMemberEmailAsync(string email, CancellationToken cancellationToken)
    {
        _ = controlPlane.ActiveTenant;
        return controlPlane.Database.SqlQuery<int>(
                $"""
                SELECT 1 AS "Value" FROM control_plane.memberships
                JOIN control_plane.users ON users.id = memberships.user_id
                WHERE lower(users.email) = lower({email})
                """)
            .AnyAsync(cancellationToken);
    }

    Task<int> ITenantRepository.CountInvitationsSinceAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
        controlPlane.TenantInvitations.CountAsync(invitation => invitation.CreatedAt > since, cancellationToken);

    // The same comparison as the partial unique index, which serves the query (K2): lower(email), pending only. Row level security
    // adds the tenant.
    async Task<Invitation?> ITenantRepository.FindPendingInvitationAsync(string email, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Invitations
            .FromSql($"SELECT * FROM control_plane.invitations WHERE lower(email) = lower({email}) AND status = 'Pending'")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
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
    async Task<bool> ITenantRepository.TryAddAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var added = OfActiveTenant(tenant, tenant.Id);
        var inserted = await controlPlane.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO control_plane.tenants (id, name, slug, status, created_at)
            VALUES ({added.Id}, {added.Name}, {added.Slug}, {added.Status.ToString()}, {added.CreatedAt}) ON CONFLICT (slug) DO NOTHING
            """,
            cancellationToken);

        return inserted == 1;
    }

    void ITenantRepository.Add(Invitation invitation) => controlPlane.Invitations.Add(invitation);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);

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

    // Row level security keeps every count to the active tenant. The latest Owner invitation is read only when the tenant has no Owner,
    // which is the only time the summary keeps it.
    private async Task<TenantSummary> ReadSummaryAsync(CancellationToken cancellationToken)
    {
        var owners = await Memberships.CountAsync(membership => membership.RoleId == BuiltInRoles.Owner.Id, cancellationToken);

        return new TenantSummary(
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
