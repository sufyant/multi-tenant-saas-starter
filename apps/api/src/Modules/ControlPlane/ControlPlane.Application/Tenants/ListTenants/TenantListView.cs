using SharedKernel;

namespace ControlPlane.Application.Tenants.ListTenants;

/// <summary>
/// A page of the platform's tenant list, how many tenants of the whole list are in each status, and how many are in a trial (4.39).
/// </summary>
public sealed record TenantListView(ListPage<TenantListItemView> Page, TenantStatusCountsView StatusCounts, int InTrialCount);
