using ControlPlane.Application.Ports;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence;

/// <summary>The catalog users in control_plane.users, which belongs to no tenant (R6). The message has no tenant, so the transaction declares none.</summary>
/// <remarks>Public only so that Wolverine's generated code can build it on the handler's own DbContext (W1, W9).</remarks>
public sealed class UserCatalog(ControlPlaneDbContext controlPlane) : IUserCatalog
{
    async Task<User?> IUserCatalog.FindUserForUpdateAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Users
            .FromSql($"SELECT * FROM control_plane.users WHERE external_id = {externalUserId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    Task IUserCatalog.SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);
}
