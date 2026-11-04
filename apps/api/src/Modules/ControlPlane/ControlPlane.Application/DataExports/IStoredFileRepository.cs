using ControlPlane.Domain.StoredFiles;

namespace ControlPlane.Application.DataExports;

/// <summary>
/// The stored files for the commands, loaded to change them and saved. A file belongs to its user (R6): each method declares the user in the
/// transaction, so the own_files policy decides what is seen (R11), and the transaction then declares no tenant (R4).
/// </summary>
/// <remarks>Public because a public handler takes it (W9).</remarks>
public interface IStoredFileRepository
{
    /// <summary>The status of the user's latest data export, without a lock; none when the user never asked for one.</summary>
    internal Task<StoredFileStatus?> FindLatestDataExportStatusAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user's file with this id, locked for the rest of the transaction; none when the user has no such file.</summary>
    internal Task<StoredFile?> FindForUpdateAsync(Guid userId, Guid fileId, CancellationToken cancellationToken);

    internal void Add(StoredFile file);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
