using SharedKernel;

namespace ControlPlane.Application.Plans.ListPlans;

/// <summary>Every plan, a page at a time, sorted by one field, then by id. The search matches the name.</summary>
[MessageName("list-plans")]
public sealed record ListPlansQuery(ListRequest<ListPlansSort> List);
