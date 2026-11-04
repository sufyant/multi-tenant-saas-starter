namespace SharedKernel;

/// <summary>
/// The object storage of the files the modules keep, such as a person's data export (section 7, "File storage"). The host implements it:
/// the S3 API of Cloudflare R2, AWS S3 or SeaweedFS. A module names a file only by its key (<see cref="FileKeys"/>), which holds ids and no
/// name or address, and keeps a row of each file in its own schema: it asks its database which files exist, never the bucket.
/// </summary>
public interface IFileStore
{
    /// <summary>How long a download link works. A link is made when the person asks for it, and never stored.</summary>
    public static readonly TimeSpan DownloadLinkLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Saves the content under the key. A file that the key names already is replaced, so a second save of a retried step is safe.</summary>
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>The content of the file under the key.</summary>
    Task<Stream> ReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Deletes the file under the key. A key without a file is done.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);

    /// <summary>Deletes every file whose key starts with the prefix, such as a user's <c>users/{userId}/</c>. A prefix without a file is done.</summary>
    Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken);

    /// <summary>
    /// A link that downloads the file under the key for <see cref="DownloadLinkLifetime"/>, as an attachment with the file name. Only the
    /// endpoint that has checked the signed-in person asks for one.
    /// </summary>
    Task<Uri> CreateDownloadLinkAsync(string key, string fileName, CancellationToken cancellationToken);
}
