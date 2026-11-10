using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Tenants.ListTenants;

/// <summary>
/// How many tenants are in each status: the tabs of the Organisations list. They are the totals of each status, without the search or
/// the status filter (the maintainer's decision of 2026-11-10).
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
