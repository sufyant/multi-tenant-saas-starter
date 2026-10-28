using ControlPlane.Application.Plans;
using ControlPlane.Application.Plans.ListPlans;
using ControlPlane.Application.Plans.GetPlanUpdatePreview;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Tenants;
using ControlPlane.Infrastructure.Persistence.Lists;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence.Plans;

/// <summary>
/// The plans for the queries, read without tracking, each answer a read model built here (Q2). Plans, the tenants, their summaries and
/// their overrides belong to no tenant (R6), so a read across tenants needs no declaration and row level security hides nothing it counts.
/// </summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class PlanQueries(ControlPlaneDbContext controlPlane) : IPlanQueries
{
    // One query for the page, which counts each plan's tenants and reads its mark; the total is a second query, as for every list.
    async Task<ListPage<PlanListItemView>> IPlanQueries.ListPlansAsync(ListRequest<ListPlansSort> list, CancellationToken cancellationToken)
    {
        var plans = controlPlane.Plans.AsQueryable();
        if (list.Search is { } search)
        {
            var pattern = ListQueryExtensions.ContainsPattern(search);
            plans = plans.Where(plan => EF.Functions.ILike(plan.Name, pattern, ListQueryExtensions.Escape));
        }

        var total = await plans.CountAsync(cancellationToken);
        var page = await plans
            .OrderBy(plan => plan.Name, list.Descending)
            .ThenBy(plan => plan.Id)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .Select(plan => new PlanListItemView(
                plan.Id,
                plan.Name,
                controlPlane.DefaultPlans.Any(mark => mark.PlanId == plan.Id),
                controlPlane.Tenants.Count(tenant => tenant.PlanId == plan.Id),
                plan.CreatedAt))
            .ToListAsync(cancellationToken);
        return ListPage.Of(page, list.Paging, total);
    }

    async Task<PlanView?> IPlanQueries.FindPlanAsync(Guid planId, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Plans
            .Where(plan => plan.Id == planId)
            .Select(plan => new
            {
                plan.Id,
                plan.Name,
                plan.CreatedAt,
                IsDefault = controlPlane.DefaultPlans.Any(mark => mark.PlanId == plan.Id),
                TenantCount = controlPlane.Tenants.Count(tenant => tenant.PlanId == plan.Id),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (found is null)
        {
            return null;
        }

        var values = await controlPlane.PlanValuesAsync(planId, cancellationToken);
        return new PlanView(
            found.Id,
            found.Name,
            found.IsDefault,
            found.TenantCount,
            values.ToDictionary(value => value.Key, value => EntitlementValueView.Of(value.Value), StringComparer.Ordinal),
            found.CreatedAt);
    }

    // The tenants on the plan, with their summaries and overrides, in three queries; the preview of each is computed here, at this moment,
    // and the page is taken from those that go over. A purged tenant has no data left to count, so it is not listed.
    async Task<ListPage<TenantExcessView>?> IPlanQueries.GetPlanUpdatePreviewAsync(
        Guid planId,
        IReadOnlyDictionary<string, EntitlementValue> values,
        ListRequest<GetPlanUpdatePreviewSort> list,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!await controlPlane.Plans.AnyAsync(plan => plan.Id == planId, cancellationToken))
        {
            return null;
        }

        var tenants = controlPlane.Tenants.Where(tenant => tenant.PlanId == planId && tenant.Status != TenantStatus.Purged);
        if (list.Search is { } search)
        {
            var pattern = ListQueryExtensions.ContainsPattern(search);
            tenants = tenants.Where(tenant =>
                EF.Functions.ILike(tenant.Name, pattern, ListQueryExtensions.Escape) || EF.Functions.ILike(tenant.Slug, pattern, ListQueryExtensions.Escape));
        }

        var onPlan = await (
                from tenant in tenants
                join kept in controlPlane.TenantSummaries on tenant.Id equals kept.TenantId into summaries
                from summary in summaries.DefaultIfEmpty()
                select new { tenant.Id, tenant.Name, tenant.Slug, Summary = summary })
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var overrides = await controlPlane.OverridesOfAsync([.. onPlan.Select(tenant => tenant.Id)], cancellationToken);
        var over = onPlan
            .Select(tenant => (Tenant: tenant, Preview: PlanPreview.Of(values, [.. overrides[tenant.Id].Select(entry => entry.ToValue())], EntitlementReads.UsageOf(tenant.Summary, now), now)))
            .Where(found => found.Preview.Excesses.Count > 0)
            .ToList();
        var sorted = list.Sort == GetPlanUpdatePreviewSort.Excess
            ? (list.Descending ? over.OrderBy(found => found.Preview.Furthest) : over.OrderByDescending(found => found.Preview.Furthest)).ThenBy(found => found.Tenant.Name, StringComparer.Ordinal)
            : (list.Descending ? over.OrderByDescending(found => found.Tenant.Name, StringComparer.Ordinal) : over.OrderBy(found => found.Tenant.Name, StringComparer.Ordinal));
        var page = sorted
            .ThenBy(found => found.Tenant.Id)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .Select(found => new TenantExcessView(
                found.Tenant.Id,
                found.Tenant.Name,
                found.Tenant.Slug,
                found.Preview.Furthest,
                [.. found.Preview.Excesses.Select(ExcessView.Of)],
                found.Preview.OverriddenKeys))
            .ToList();
        return ListPage.Of(page, list.Paging, over.Count);
    }
}
