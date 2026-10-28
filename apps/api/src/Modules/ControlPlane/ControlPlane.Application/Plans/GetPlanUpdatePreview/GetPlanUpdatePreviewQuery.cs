using SharedKernel;

namespace ControlPlane.Application.Plans.GetPlanUpdatePreview;

/// <summary>
/// Before a plan edit: the tenants on the plan that these values would put over a limit, or whose feature in use they would turn off, with
/// each key, the count and the new value. A page at a time; the search matches the tenant's name or slug. It never blocks the edit.
/// </summary>
[MessageName("get-plan-update-preview")]
public sealed record GetPlanUpdatePreviewQuery(Guid PlanId, IReadOnlyDictionary<string, EntitlementValueView> Entitlements, ListRequest<GetPlanUpdatePreviewSort> List);
