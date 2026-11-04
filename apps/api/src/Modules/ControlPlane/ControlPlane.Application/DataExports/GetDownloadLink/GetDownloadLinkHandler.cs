using SharedKernel;

namespace ControlPlane.Application.DataExports.GetDownloadLink;

// The handler takes no DbContext, so Wolverine opens no transaction around the call to the file store (section 3).
public static class GetDownloadLinkHandler
{
    public static async Task<DownloadLink> HandleAsync(GetDownloadLinkQuery query, IFileStore files, TimeProvider time, CancellationToken cancellationToken)
    {
        var expiresAt = time.GetUtcNow() + IFileStore.DownloadLinkLifetime;
        return new DownloadLink(await files.CreateDownloadLinkAsync(query.ObjectKey, query.FileName, cancellationToken), expiresAt);
    }
}
