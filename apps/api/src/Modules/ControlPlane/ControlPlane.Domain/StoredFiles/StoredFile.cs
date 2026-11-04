using SharedKernel;

namespace ControlPlane.Domain.StoredFiles;

/// <summary>
/// One file that ControlPlane keeps in the file store, and the one record that it exists: the module asks this table which files exist,
/// never the bucket (section 7, "File storage"). The row keeps the object key, never a bucket, an endpoint or a link. Today every file is a
/// person's data export (<c>docs/flows/personal-data-export.md</c>). The row belongs to its user, not to a tenant (R6, R11).
/// </summary>
internal sealed class StoredFile(Guid id, Guid userId, string module, StoredFilePurpose purpose, string objectKey, string contentType)
{
    /// <summary>The module that keeps the files of this table.</summary>
    public const string ControlPlaneModule = "control_plane";

    public const string ZipContentType = "application/zip";

    /// <summary>How long a ready export can be downloaded. Then ControlPlane deletes its file, and the bucket's lifecycle rule is the second guard.</summary>
    public static readonly TimeSpan DataExportLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// How long an export may stay in preparation. An export still preparing then has failed for good, and the person can ask again: a
    /// healthy export ends in seconds, and an hour also covers a worker that restarts or a deploy, as the tenant purge's timeout does.
    /// </summary>
    public static readonly TimeSpan DataExportPreparationTimeout = TimeSpan.FromHours(1);

    public Guid Id { get; private init; } = id;

    public Guid UserId { get; private init; } = userId;

    public string Module { get; private init; } = module;

    public StoredFilePurpose Purpose { get; private init; } = purpose;

    /// <summary>The key of the file in the store (<see cref="FileKeys"/>): ids only, never a name or an address.</summary>
    public string ObjectKey { get; private init; } = objectKey;

    public string ContentType { get; private init; } = contentType;

    /// <summary>The size of the file in bytes, once it is ready.</summary>
    public long? SizeBytes { get; private set; }

    public StoredFileStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset? ReadyAt { get; private set; }

    /// <summary>Until when a ready file can be downloaded; it stays after the file expired, so the screen says when it was deleted.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>The name the person's download gives the file: the day of the request, in UTC, and no name or address.</summary>
    public string FileName => $"data-export-{CreatedAt.UtcDateTime:yyyy-MM-dd}.zip";

    /// <summary>The person asks for their data at this moment. The worker prepares the file.</summary>
    public static StoredFile PrepareDataExport(Guid id, Guid userId, DateTimeOffset now) =>
        new(id, userId, ControlPlaneModule, StoredFilePurpose.DataExport, FileKeys.UserExport(userId, id), ZipContentType)
        {
            Status = StoredFileStatus.Preparing,
            CreatedAt = now,
        };

    /// <summary>The worker saved the file at this moment. It can be downloaded until the end of its lifetime. Changed only for a file in preparation.</summary>
    public StateChange Ready(long sizeBytes, DateTimeOffset now)
    {
        if (Status != StoredFileStatus.Preparing)
        {
            return StateChange.Unchanged;
        }

        (Status, SizeBytes, ReadyAt, ExpiresAt) = (StoredFileStatus.Ready, sizeBytes, now, now + DataExportLifetime);
        return StateChange.Changed;
    }

    /// <summary>The preparation did not end within its timeout. Changed only for a file in preparation.</summary>
    public StateChange Fail()
    {
        if (Status != StoredFileStatus.Preparing)
        {
            return StateChange.Unchanged;
        }

        Status = StoredFileStatus.Failed;
        return StateChange.Changed;
    }

    /// <summary>The lifetime of a ready file ended: ControlPlane deletes its file. Changed only for a ready file.</summary>
    public StateChange Expire()
    {
        if (Status != StoredFileStatus.Ready)
        {
            return StateChange.Unchanged;
        }

        Status = StoredFileStatus.Expired;
        return StateChange.Changed;
    }
}
