using ControlPlane.Application.Plans.ListPlans;
using ControlPlane.Application.Plans.GetPlanUpdatePreview;
using ControlPlane.Domain.Plans;
using SharedKernel;

namespace ControlPlane.Application.Plans;

/// <summary>
/// The platform's reads of the plans (Q2). Plans, their values, the tenants' plans and overrides and the tenants' summaries belong to no
/// tenant (R6), so a read across tenants sees every row it needs, and row level security hides nothing it counts.
/// </summary>
/// <remarks>Public because a public handler takes it (W9); its members are internal to the module.</remarks>
public interface IPlanQueries
{
    internal Task<ListPage<PlanListItemView>> ListPlansAsync(ListRequest<ListPlansSort> list, CancellationToken cancellationToken);

    /// <summary>The plan with this id, or none.</summary>
    internal Task<PlanView?> FindPlanAsync(Guid planId, CancellationToken cancellationToken);

    /// <summary>
    /// The tenants on the plan, but a purged tenant, that these values would put over a limit, or whose feature in use they would turn
    /// off, under each tenant's overrides at this moment: a page of them, those that go furthest over first. The search matches the
    /// tenant's name or slug. None when no plan has the id.
    /// </summary>
    internal Task<ListPage<TenantExcessView>?> GetPlanUpdatePreviewAsync(
        Guid planId,
        IReadOnlyDictionary<string, EntitlementValue> values,
        ListRequest<GetPlanUpdatePreviewSort> list,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
