using ControlPlane.Application.DataExports;
using ControlPlane.Domain.StoredFiles;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.DataExports;

/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class DataExportQueries(ControlPlaneDbContext controlPlane) : IDataExportQueries
{
    // The person's id first, without the declaration, because the users table belongs to no user (R6); then their latest export, which
    // the own_files policy shows only to them (R11), in the handler's transaction.
    async Task<DataExportView?> IDataExportQueries.FindLatestAsync(string externalUserId, CancellationToken cancellationToken)
    {
        if (await controlPlane.Users.Where(user => user.ExternalId == externalUserId).Select(user => (Guid?)user.Id).SingleOrDefaultAsync(cancellationToken) is not { } userId)
        {
            return null;
        }

        await controlPlane.DeclareUserAsync(userId, cancellationToken);
        var latest = await controlPlane.StoredFiles
            .AsNoTracking()
            .Where(file => file.UserId == userId && file.Purpose == StoredFilePurpose.DataExport)
            .OrderByDescending(file => file.CreatedAt)
            .ThenByDescending(file => file.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return latest is null ? null : DataExportView.Of(latest);
    }
}
