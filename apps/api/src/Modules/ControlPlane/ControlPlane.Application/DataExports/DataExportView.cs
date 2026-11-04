using ControlPlane.Domain.StoredFiles;

namespace ControlPlane.Application.DataExports;

/// <summary>
/// A data export as the person's Account and security reads it: where it is, when they asked, when it was ready and until when they can
/// download it, which stays after it expired, so the screen says when the file was deleted.
/// </summary>
public sealed record DataExportView(Guid Id, string Status, DateTimeOffset RequestedAt, DateTimeOffset? ReadyAt, DateTimeOffset? ExpiresAt, long? SizeBytes)
{
    internal static DataExportView Of(StoredFile file) =>
        new(file.Id, file.Status.ToString(), file.CreatedAt, file.ReadyAt, file.ExpiresAt, file.SizeBytes);
}
