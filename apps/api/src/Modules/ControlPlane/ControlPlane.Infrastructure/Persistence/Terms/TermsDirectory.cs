using ControlPlane.Contracts.Terms;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Terms;

/// <summary>The users the terms concern, for other modules. The users belong to no tenant (R6).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class TermsDirectory(ControlPlaneDbContext controlPlane) : ITermsDirectory
{
    public async Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(CancellationToken cancellationToken) =>
        await controlPlane.Users.Where(TermsQueries.MayAccept).OrderBy(user => user.CreatedAt).ThenBy(user => user.Id).Select(user => user.Id).ToListAsync(cancellationToken);
}
