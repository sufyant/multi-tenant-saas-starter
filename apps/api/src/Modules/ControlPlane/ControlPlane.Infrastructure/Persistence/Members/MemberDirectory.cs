using ControlPlane.Contracts.Members;
using ControlPlane.Domain;
using ControlPlane.Domain.Roles;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Members;

// Reads the members of the tenant that the handler's transaction declares, from its envelope (W2): row level security keeps the read to
// that tenant (R1), so it takes no tenant id. Public because Wolverine's generated code builds it on the handler's DbContext (W1, W9).
public sealed class MemberDirectory(ControlPlaneDbContext controlPlane) : IMemberDirectory
{
    public Task<IReadOnlyList<Guid>> ListOwnerIdsAsync(CancellationToken cancellationToken) =>
        ListHoldingAsync(PermissionPools.TenantOwnersManage, cancellationToken);

    public Task<IReadOnlyList<Guid>> ListBillingManagerIdsAsync(CancellationToken cancellationToken) =>
        ListHoldingAsync(PermissionPools.TenantBillingManage, cancellationToken);

    // The one predicate of the free seat (Role.TakingNoSeat), as the seat count of the tenant's methods reads it.
    public async Task<int> CountSeatedMembersAsync(CancellationToken cancellationToken)
    {
        var seatless = controlPlane.Roles.Where(Role.TakingNoSeat).Select(role => role.Id);
        return await controlPlane.Memberships.CountAsync(membership => !seatless.Contains(membership.RoleId), cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> ListHoldingAsync(string permission, CancellationToken cancellationToken) =>
        await (
                from membership in controlPlane.Memberships
                join role in controlPlane.Roles.Where(Role.Holding(permission)) on membership.RoleId equals role.Id
                orderby membership.UserId
                select membership.UserId)
            .ToListAsync(cancellationToken);
}
