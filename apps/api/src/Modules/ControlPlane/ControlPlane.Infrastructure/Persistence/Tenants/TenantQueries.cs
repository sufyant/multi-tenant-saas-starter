using ControlPlane.Application.Plans;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.GetMyTenant;
using ControlPlane.Application.Tenants.GetTenant;
using ControlPlane.Application.Tenants.GetTenantPlan;
using ControlPlane.Application.Tenants.ListMyTenants;
using ControlPlane.Application.Tenants.ListTenants;
using ControlPlane.Application.Tenants.GetTenantPlanChangePreview;
using ControlPlane.Application.Users;
using ControlPlane.Domain;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using ControlPlane.Infrastructure.Persistence.Lists;
using ControlPlane.Infrastructure.Persistence.Plans;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

/// <summary>
/// The tenants for the queries, read without tracking, each answer a read model built here (Q2). One tenant is read inside the active tenant (W2); the lists run outside any
/// tenant: the platform's list sees every tenant, and a user's list declares the user (R11), so the own_memberships policy decides what
/// is seen. The message of a user's list has no tenant, so the transaction declares none (R4).
/// </summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class TenantQueries(ControlPlaneDbContext controlPlane) : ITenantQueries
{
    async Task<TenantView?> ITenantQueries.FindTenantAsync(CancellationToken cancellationToken) =>
        await FindAsync(cancellationToken) is { } tenant ? TenantView.Of(tenant) : null;

    // Two queries, as before the views: the tenant with its summary joined (R12), then its Owners from its own rows. A purged tenant has
    // no summary, so the summary is joined where it exists, and a tombstone counts nothing (K4).
    async Task<TenantDetailView?> ITenantQueries.FindTenantDetailAsync(
        TimeSpan reopenPeriod,
        TimeSpan purgeWarningLead,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var found = await (
                from tenant in controlPlane.Tenants
                where tenant.Id == controlPlane.ActiveTenant
                join kept in controlPlane.TenantSummaries on tenant.Id equals kept.TenantId into summaries
                from summary in summaries.DefaultIfEmpty()
                select new { Tenant = tenant, Summary = summary })
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (found is null)
        {
            return null;
        }

        var owners = await ListOwnersAsync(cancellationToken);
        return new TenantDetailView(
            TenantView.Of(found.Tenant),
            found.Tenant.CreatedAt,
            found.Tenant.ReopenableUntil(reopenPeriod),
            found.Tenant.PurgeWarningAt(reopenPeriod, purgeWarningLead),
            found.Summary?.MemberCount ?? 0,
            found.Summary?.OwnerCount ?? 0,
            [.. owners.Select(TenantOwnerView.Of)],
            found.Summary is { } stored ? OwnerInvitationView.Of(stored, now) : null);
    }

    // The tenant, the person and their membership's role with its permissions, in three queries; row level security keeps the membership
    // and the role's permissions to the active tenant. A custom role's permissions are its rows (A3). Then the tenant's plan, its values,
    // overrides and summary, which belong to no tenant (R6): the effective values at this moment, and each limit's count.
    async Task<MyTenantDetailView?> ITenantQueries.FindMyTenantAsync(string externalUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await FindAsync(cancellationToken) is not { } tenant
            || await controlPlane.Users.AsNoTracking().SingleOrDefaultAsync(user => user.ExternalId == externalUserId, cancellationToken) is not { } user
            || await (
                    from membership in controlPlane.TenantMemberships
                    where membership.UserId == user.Id
                    join found in controlPlane.Roles on membership.RoleId equals found.Id
                    select found)
                .Include(found => found.Grants)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken) is not { } role)
        {
            return null;
        }

        var facts = await FindPlanFactsAsync(cancellationToken) ?? throw new InvalidOperationException("The tenant was found in this transaction.");
        var plan = await controlPlane.PlanValuesAsync(facts.Plan.Id, cancellationToken);
        var overrides = (await controlPlane.OverridesOfAsync([facts.TenantId], cancellationToken))[facts.TenantId].Select(entry => entry.ToValue());
        var effective = EffectiveEntitlements.Of(plan, overrides, now);
        var usage = EntitlementReads.UsageOf(facts.Summary, now);
        return new MyTenantDetailView(
            MyTenantView.Of(tenant, role),
            [.. role.Permissions.Order(StringComparer.Ordinal)],
            tenant.CreatedAt,
            facts.Plan.Name,
            effective.Values.OrderBy(value => value.Key, StringComparer.Ordinal).ToDictionary(value => value.Key, value => EntitlementValueView.Of(value.Value), StringComparer.Ordinal),
            EntitlementKeys.Limits.ToDictionary(key => key, usage.CountOf, StringComparer.Ordinal));
    }

    // The tenants table and their summaries belong to no tenant, so the list needs no active tenant and sees every tenant. The page joins
    // each tenant's summary (R12): one query, with no query for each tenant. A purged tenant has no summary, so the summary is joined where
    // it exists, and a tombstone counts nothing (K4).
    async Task<ListPage<TenantListItemView>> ITenantQueries.ListTenantsAsync(
        ListRequest<ListTenantsSort> list,
        TenantStatus? status,
        TimeSpan reopenPeriod,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tenants = controlPlane.Tenants.AsQueryable();
        if (status is { } wanted)
        {
            tenants = tenants.Where(tenant => tenant.Status == wanted);
        }

        if (list.Search is { } search)
        {
            var pattern = ListQueryExtensions.ContainsPattern(search);
            tenants = tenants.Where(tenant =>
                EF.Functions.ILike(tenant.Name, pattern, ListQueryExtensions.Escape) || EF.Functions.ILike(tenant.Slug, pattern, ListQueryExtensions.Escape));
        }

        var total = await tenants.CountAsync(cancellationToken);
        var rows =
            from tenant in tenants
            join kept in controlPlane.TenantSummaries on tenant.Id equals kept.TenantId into summaries
            from summary in summaries.DefaultIfEmpty()
            select new { Tenant = tenant, Summary = summary };
        var sorted = list.Sort switch
        {
            ListTenantsSort.Slug => rows.OrderBy(row => row.Tenant.Slug, list.Descending),
            _ => rows.OrderBy(row => row.Tenant.Name, list.Descending),
        };
        var page = await sorted
            .ThenBy(row => row.Tenant.Id)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ListPage.Of(
            [
                .. page.Select(row => new TenantListItemView(
                    TenantView.Of(row.Tenant),
                    row.Tenant.CreatedAt,
                    row.Tenant.ReopenableUntil(reopenPeriod),
                    row.Summary?.MemberCount ?? 0,
                    row.Summary?.OwnerCount ?? 0,
                    row.Summary is { } stored ? OwnerInvitationView.Of(stored, now) : null)),
            ],
            list.Paging,
            total);
    }

    // The user is found by the identity provider's id in control_plane.users, which belongs to no tenant. The page is one query:
    // each membership's tenant and role are joined, with no query for each tenant.
    // The tenant row, its plan, its overrides with the user who set each one, and its summary: the tables belong to no tenant (R6).
    async Task<TenantPlanView?> ITenantQueries.FindTenantPlanAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await FindPlanFactsAsync(cancellationToken) is not { } found)
        {
            return null;
        }

        var plan = await controlPlane.PlanValuesAsync(found.Plan.Id, cancellationToken);
        var overrides = await (
                from entry in controlPlane.EntitlementOverrides
                where entry.TenantId == found.TenantId
                join user in controlPlane.Users on entry.SetBy equals user.Id
                select new { Entry = entry, User = user })
            .AsNoTracking()
            .ToDictionaryAsync(pair => pair.Entry.Key, StringComparer.Ordinal, cancellationToken);
        var effective = EffectiveEntitlements.Of(plan, overrides.Values.Select(pair => pair.Entry.ToValue()), now);
        var usage = EntitlementReads.UsageOf(found.Summary, now);
        var entitlements = EntitlementKeys.All
            .OrderBy(key => key.Key, StringComparer.Ordinal)
            .Select(key => new TenantEntitlementView(
                key.Key,
                key.Value == EntitlementKind.Feature,
                plan.TryGetValue(key.Key, out var planValue) ? EntitlementValueView.Of(planValue) : null,
                overrides.TryGetValue(key.Key, out var pair)
                    ? new EntitlementOverrideView(
                        EntitlementValueView.Of(pair.Entry.Value),
                        pair.Entry.ExpiresAt,
                        !pair.Entry.ToValue().IsActiveAt(now),
                        pair.Entry.Reason,
                        pair.User.Id,
                        UserNameView.Of(pair.User),
                        pair.Entry.SetAt)
                    : null,
                EntitlementValueView.Of(effective.ValueOf(key.Key)),
                usage.CountOf(key.Key)))
            .ToList();
        return new TenantPlanView(found.TenantId, found.Plan, entitlements);
    }

    async Task<TenantPlanChangePreviewView?> ITenantQueries.PreviewPlanChangeAsync(
        IReadOnlyDictionary<string, EntitlementValue> plan,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await FindPlanFactsAsync(cancellationToken) is not { } found)
        {
            return null;
        }

        var overrides = (await controlPlane.OverridesOfAsync([found.TenantId], cancellationToken))[found.TenantId].Select(entry => entry.ToValue()).ToList();
        var preview = PlanPreview.Of(plan, overrides, EntitlementReads.UsageOf(found.Summary, now), now);
        return new TenantPlanChangePreviewView([.. preview.Excesses.Select(ExcessView.Of)], preview.OverriddenKeys);
    }

    async Task<ListPage<MyTenantView>> ITenantQueries.ListMyTenantsAsync(string externalUserId, ListRequest<ListMyTenantsSort> list, CancellationToken cancellationToken)
    {
        var userId = await controlPlane.Users
            .Where(user => user.ExternalId == externalUserId)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is not { } id)
        {
            return ListPage.Of<MyTenantView>([], list.Paging, 0);
        }

        await controlPlane.DeclareUserAsync(id, cancellationToken);
        var tenants =
            from membership in controlPlane.Memberships
            join tenant in controlPlane.Tenants on membership.TenantId equals tenant.Id
            join role in controlPlane.Roles on membership.RoleId equals role.Id
            select new { Tenant = tenant, Role = role };
        if (list.Search is { } search)
        {
            var pattern = ListQueryExtensions.ContainsPattern(search);
            tenants = tenants.Where(row =>
                EF.Functions.ILike(row.Tenant.Name, pattern, ListQueryExtensions.Escape) || EF.Functions.ILike(row.Tenant.Slug, pattern, ListQueryExtensions.Escape));
        }

        // The list has one sort field, the name.
        var total = await tenants.CountAsync(cancellationToken);
        var page = await tenants
            .OrderBy(row => row.Tenant.Name, list.Descending)
            .ThenBy(row => row.Tenant.Id)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ListPage.Of([.. page.Select(row => MyTenantView.Of(row.Tenant, row.Role))], list.Paging, total);
    }

    // Row level security keeps the memberships to the active tenant; the users table is not under it (R6), so the query starts from them.
    // An Owner is a member whose role holds the Owners' permission (A1, K1).
    private async Task<IReadOnlyList<User>> ListOwnersAsync(CancellationToken cancellationToken) =>
        await (
                from membership in controlPlane.TenantMemberships
                join role in controlPlane.Roles.Where(Role.Holding(PermissionPools.TenantOwnersManage)) on membership.RoleId equals role.Id
                join user in controlPlane.Users on membership.UserId equals user.Id
                orderby membership.JoinedAt, membership.UserId
                select user)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    private Task<Tenant?> FindAsync(CancellationToken cancellationToken) =>
        controlPlane.Tenants.AsNoTracking().SingleOrDefaultAsync(tenant => tenant.Id == controlPlane.ActiveTenant, cancellationToken);

    // The active tenant with its plan's id and name and its summary, where it has one: a purged tenant's tombstone has none (K4).
    private async Task<(Guid TenantId, PlanNames Plan, TenantSummary? Summary)?> FindPlanFactsAsync(CancellationToken cancellationToken)
    {
        var found = await (
                from tenant in controlPlane.Tenants
                where tenant.Id == controlPlane.ActiveTenant
                join plan in controlPlane.Plans on tenant.PlanId equals plan.Id
                join kept in controlPlane.TenantSummaries on tenant.Id equals kept.TenantId into summaries
                from summary in summaries.DefaultIfEmpty()
                select new { tenant.Id, PlanId = plan.Id, PlanName = plan.Name, Summary = summary })
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return found is null ? null : (found.Id, new PlanNames(found.PlanId, found.PlanName), found.Summary);
    }
}
