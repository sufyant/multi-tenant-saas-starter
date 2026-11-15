using ControlPlane.Contracts.Tenants;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

// control_plane.tenants belongs to no tenant (R6), so no declaration is needed. Public because Wolverine's generated code builds it on
// the caller's ControlPlane DbContext (W1, W9).
public sealed class TenantNames(ControlPlaneDbContext controlPlane) : ITenantNames
{
    public async Task<string?> FindNameAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await controlPlane.Tenants.Where(tenant => tenant.Id == tenantId).Select(tenant => tenant.Name).SingleOrDefaultAsync(cancellationToken);
}
