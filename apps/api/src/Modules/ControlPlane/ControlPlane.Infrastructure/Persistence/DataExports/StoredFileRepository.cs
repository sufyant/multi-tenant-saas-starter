using ControlPlane.Application.DataExports;
using ControlPlane.Domain.Files;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.DataExports;

/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class StoredFileRepository(ControlPlaneDbContext controlPlane) : IStoredFileRepository
{
    async Task<StoredFile?> IStoredFileRepository.FindLatestDataExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        await controlPlane.DeclareUserAsync(userId, cancellationToken);
        return await controlPlane.StoredFiles
            .Where(file => file.UserId == userId && file.Purpose == StoredFilePurpose.DataExport)
            .OrderByDescending(file => file.CreatedAt)
            .ThenByDescending(file => file.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    async Task<StoredFile?> IStoredFileRepository.FindForUpdateAsync(Guid userId, Guid fileId, CancellationToken cancellationToken)
    {
        await controlPlane.DeclareUserAsync(userId, cancellationToken);
        var found = await controlPlane.StoredFiles
            .FromSql($"SELECT * FROM control_plane.stored_files WHERE id = {fileId} AND user_id = {userId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return found.SingleOrDefault();
    }

    void IStoredFileRepository.Add(StoredFile file) => controlPlane.StoredFiles.Add(file);

    Task IStoredFileRepository.SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);
}
