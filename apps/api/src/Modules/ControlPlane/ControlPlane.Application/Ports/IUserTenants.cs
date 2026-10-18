using ControlPlane.Application.Tenants.ListMyTenants;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Ports;

/// <summary>
/// The tenants a user belongs to, read outside any tenant (T4): through the user's own memberships, which the database lets the
/// declared user read in every tenant (R11).
/// </summary>
/// <remarks>Public only because Wolverine's generated code passes it to public handlers.</remarks>
public interface IUserTenants
{
    /// <summary>
    /// A page of the active, suspended and closed tenants the user is a member of, each with the user's role there, whose name or slug
    /// contains the search, by the sort field, then id; none for a user the catalog does not know.
    /// </summary>
    internal Task<ListPage<(Tenant Tenant, Role Role)>> ListPastCreationAsync(string externalUserId, ListRequest<ListMyTenantsSort> list, CancellationToken cancellationToken);
}
