using ControlPlane.Contracts.TermsVersions;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.TermsVersions;

/// <summary>The users the terms concern, for other modules. The users belong to no tenant (R6).</summary>
/// <remarks>Public because Wolverine's generated code builds it in Notifications' handler, on this module's DbContext (W1, W9).</remarks>
public sealed class TermsVersionDirectory(ControlPlaneDbContext controlPlane) : ITermsVersionDirectory
{
    public async Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(CancellationToken cancellationToken) =>
        await controlPlane.Users.Where(TermsVersionQueries.MayAccept).OrderBy(user => user.CreatedAt).ThenBy(user => user.Id).Select(user => user.Id).ToListAsync(cancellationToken);
}
