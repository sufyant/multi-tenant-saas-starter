using ControlPlane.Application.Tenants.ListMyTenants;
using ControlPlane.Application.Tenants.ListTenants;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// The tenants for the queries, read without tracking: one tenant inside it, and the lists outside any tenant. It never loads a tenant
/// to change it; <see cref="ITenantRepository"/> does.
/// </summary>
/// <remarks>Public only because Wolverine's generated code passes it to public handlers.</remarks>
public interface ITenantQueries
{
    /// <summary>The active tenant, without a lock, for a read. Null when no tenant has the active tenant's id.</summary>
    internal Task<Tenant?> FindTenantAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A page of every tenant whose name or slug contains the search, in the status or every status, by the sort field, then id. It
    /// needs no active tenant.
    /// </summary>
    internal Task<ListPage<(Tenant Tenant, User? FirstOwner)>> ListTenantsAsync(ListRequest<ListTenantsSort> list, TenantStatus? status, CancellationToken cancellationToken);

    /// <summary>The users who hold the Owner role in the active tenant, in the order they joined, then by user id.</summary>
    internal Task<IReadOnlyList<User>> ListOwnersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A page of the active, suspended and closed tenants the user is a member of, each with the user's role there, whose name or slug
    /// contains the search, by the sort field, then id; none for a user the catalog does not know. It runs outside any tenant (T4),
    /// through the user's own memberships, which the database lets the declared user read in every tenant (R11).
    /// </summary>
    internal Task<ListPage<(Tenant Tenant, Role Role)>> ListPastCreationAsync(string externalUserId, ListRequest<ListMyTenantsSort> list, CancellationToken cancellationToken);
}
