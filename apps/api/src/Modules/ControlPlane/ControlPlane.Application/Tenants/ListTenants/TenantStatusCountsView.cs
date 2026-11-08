using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Tenants.ListTenants;

/// <summary>
/// How many tenants of the list are in each status: the tabs of the Organisations list. They follow the search and ignore the status
/// filter, as the Open and Closed counts of GitHub Issues do (the maintainer's decision of 2026-11-08).
/// </summary>
public sealed record TenantStatusCountsView(int Active, int Suspended, int Closed, int Purging, int Purged)
{
    internal static TenantStatusCountsView Of(IReadOnlyDictionary<TenantStatus, int> counts) => new(
        counts.GetValueOrDefault(TenantStatus.Active),
        counts.GetValueOrDefault(TenantStatus.Suspended),
        counts.GetValueOrDefault(TenantStatus.Closed),
        counts.GetValueOrDefault(TenantStatus.Purging),
        counts.GetValueOrDefault(TenantStatus.Purged));
}
