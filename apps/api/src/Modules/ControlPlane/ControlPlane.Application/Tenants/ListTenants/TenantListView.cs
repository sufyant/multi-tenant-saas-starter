using SharedKernel;

namespace ControlPlane.Application.Tenants.ListTenants;

/// <summary>A page of the platform's tenant list, and how many tenants of the whole list are in each status.</summary>
public sealed record TenantListView(ListPage<TenantListItemView> Page, TenantStatusCountsView StatusCounts);
