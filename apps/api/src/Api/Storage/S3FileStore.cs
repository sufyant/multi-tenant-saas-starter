using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Api.Storage;

// The file store on the S3 API, which Cloudflare R2, AWS S3 and SeaweedFS all speak (section 7, "File storage"). It knows the bucket; a module
// knows only keys. A key without a file is done for a deletion, so a retried step is safe. A prefix is deleted by listing its keys, a page
// of at most 1000 at a time, the most one request deletes: the S3 API has no deletion of a prefix.
internal sealed class S3FileStore(IAmazonS3 s3, IOptions<StorageOptions> settings, TimeProvider time) : IFileStore
{
    private readonly string _bucket = settings.Value.Bucket!;

    // The SDK signs a link for https unless told otherwise; a local S3 server answers on http.
    private readonly Protocol _protocol = settings.Value.Endpoint!.Scheme == Uri.UriSchemeHttp ? Protocol.HTTP : Protocol.HTTPS;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        // R2 implements neither the SDK's chunked signing of a body (STREAMING-AWS4-HMAC-SHA256-PAYLOAD) nor a trailing checksum (Cloudflare
        // R2 documentation, "S3 API compatibility"). So the SDK signs the whole body in one part: the header carries its SHA-256, which the buffer
        // lets the SDK compute before it sends the body. Cloudflare's example turns the body's signing off instead, but the SDK allows that on
        // https only, so local runs and the tests on SeaweedFS, on http, would send another request than production.
        using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;
        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = buffered,
                ContentType = contentType,
                AutoCloseStream = false,
                UseChunkEncoding = false,
            },
            cancellationToken);
    }

    public async Task<Stream> ReadAsync(string key, CancellationToken cancellationToken)
    {
        using var response = await s3.GetObjectAsync(_bucket, key, cancellationToken);
        var content = new MemoryStream();
        await response.ResponseStream.CopyToAsync(content, cancellationToken);
        content.Position = 0;
        return content;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        s3.DeleteObjectAsync(_bucket, key, cancellationToken);

    public async Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        while (true)
        {
            var page = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket, Prefix = prefix, MaxKeys = 1000 }, cancellationToken);
            if (page.S3Objects is not { Count: > 0 } objects)
            {
                return;
            }

            await s3.DeleteObjectsAsync(
                new DeleteObjectsRequest { BucketName = _bucket, Objects = [.. objects.Select(file => new KeyVersion { Key = file.Key })] },
                cancellationToken);
        }
    }

    public Task<Uri> CreateDownloadLinkAsync(string key, string fileName, CancellationToken cancellationToken)
    {
        var link = s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Protocol = _protocol,
            Expires = time.GetUtcNow().Add(IFileStore.DownloadLinkLifetime).UtcDateTime,
            ResponseHeaderOverrides = { ContentDisposition = $"attachment; filename=\"{fileName}\"" },
        });
        return Task.FromResult(new Uri(link));
    }
}
