using ControlPlane.Contracts.Members;
using ControlPlane.Domain;
using ControlPlane.Domain.Roles;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Members;

// Reads the members of the tenant that the handler's transaction declares, from its envelope (W2): row level security keeps the read to
// that tenant (R1), so it takes no tenant id. A built-in role's permissions come from code, a custom role's from its rows (A2, A3). Public
// because Wolverine's generated code builds it on the handler's DbContext (W1, W9).
public sealed class MemberDirectory(ControlPlaneDbContext controlPlane) : IMemberDirectory
{
    public async Task<IReadOnlyList<Guid>> ListOwnerIdsAsync(CancellationToken cancellationToken) =>
        await (
                from membership in controlPlane.Memberships
                join role in controlPlane.Roles.Where(Role.Holding(PermissionPools.TenantOwnersManage)) on membership.RoleId equals role.Id
                orderby membership.UserId
                select membership.UserId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<string>?> FindPermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var role = await (
                from membership in controlPlane.Memberships
                join found in controlPlane.Roles on membership.RoleId equals found.Id
                where membership.UserId == userId
                select found)
            .Include(found => found.Grants)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        return role switch
        {
            null => null,
            { BuiltIn: { } builtIn } => BuiltInRoles.PermissionsOf(builtIn),
            _ => role.Grants.Select(grant => grant.Permission).ToHashSet(StringComparer.Ordinal),
        };
    }
}
