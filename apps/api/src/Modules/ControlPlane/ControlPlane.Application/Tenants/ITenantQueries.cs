using ControlPlane.Application.Tenants.GetMyTenant;
using ControlPlane.Application.Tenants.GetTenant;
using ControlPlane.Application.Tenants.ListMyTenants;
using ControlPlane.Application.Tenants.ListTenants;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// The tenants for the queries: each method reads without tracking and answers a read model, built in the query (Q2). One tenant is read
/// inside the active tenant; the lists run outside any tenant. It never loads a tenant to change it; <see cref="ITenantRepository"/> does.
/// </summary>
/// <remarks>Public because a public handler takes it (W9).</remarks>
public interface ITenantQueries
{
    /// <summary>The active tenant; null when no tenant has the active tenant's id.</summary>
    internal Task<TenantView?> FindTenantAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The active tenant for the platform, with its summary (R12), until when it can be reopened, when its Owners are warned of its purge,
    /// and its Owners in the order they joined, then by user id; null when no tenant has the active tenant's id.
    /// </summary>
    internal Task<TenantDetailView?> FindTenantDetailAsync(
        TimeSpan reopenPeriod,
        TimeSpan purgeWarningLead,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// The active tenant as the member with this identity provider id sees it, with their role and its permissions; null when no tenant
    /// has the active tenant's id, the person is not a user, or they are no member of it (T3). It carries the most custom roles the tenant
    /// may have, as given.
    /// </summary>
    internal Task<MyTenantDetailView?> FindMyTenantAsync(string externalUserId, int maxCustomRoles, CancellationToken cancellationToken);

    /// <summary>
    /// A page of every tenant whose name or slug contains the search, in the status or every status, by the sort field, then id. It
    /// needs no active tenant.
    /// </summary>
    internal Task<ListPage<TenantListItemView>> ListTenantsAsync(
        ListRequest<ListTenantsSort> list,
        TenantStatus? status,
        TimeSpan reopenPeriod,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// A page of the active, suspended and closed tenants the user is a member of, each with the user's role there, whose name or slug
    /// contains the search, by the sort field, then id; none for an unknown user. It runs outside any tenant (T4),
    /// through the user's own memberships, which the database lets the declared user read in every tenant (R11).
    /// </summary>
    internal Task<ListPage<MyTenantView>> ListMyTenantsAsync(string externalUserId, ListRequest<ListMyTenantsSort> list, CancellationToken cancellationToken);
}
