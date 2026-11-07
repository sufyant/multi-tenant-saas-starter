using System.Net;
using System.Security.Cryptography;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Api.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Api.UnitTests.Storage;

// Section 7, "File storage", rule 1: the requests that the host's file store sends, as the host registers it, to an https endpoint, as R2's
// is. SeaweedFS accepts what R2 refuses, so the integration tests cannot see it: these tests pin the requests. No request leaves the process.
public sealed class S3FileStoreTests : IDisposable
{
    private readonly RecordingHandler _r2 = new();

    public void Dispose() => _r2.Dispose();

    [Fact]
    public async Task Save_AFile_SignsItsWholeBodyInOnePart()
    {
        await FileStore().SaveAsync("exports/users/a/b.zip", new MemoryStream(Encoding.UTF8.GetBytes("zip")), "application/zip", TestContext.Current.CancellationToken);

        var put = _r2.Requests.ShouldHaveSingleItem();
        put.Method.ShouldBe(HttpMethod.Put);
        put.ContentSha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData("zip"u8)));
        put.Trailer.ShouldBeNull();
        put.ContentEncoding.ShouldNotContain("aws-chunked");
        put.ContentLength.ShouldBe(3);
    }

    [Fact]
    public async Task ReadDeleteAndDeletePrefix_AnyFile_SendNoChunkedBodyAndNoTrailingChecksum()
    {
        var files = FileStore();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var content = await files.ReadAsync("users/a/file", cancellationToken);
        await files.DeleteAsync("users/a/file", cancellationToken);
        await files.DeletePrefixAsync("users/a/", cancellationToken);

        _r2.Requests.Select(request => request.Method.Method).ShouldBe(["GET", "DELETE", "GET", "POST", "GET"]);
        _r2.Requests.ShouldAllBe(request => !request.ContentSha256!.StartsWith("STREAMING-") && request.Trailer == null);
    }

    private IFileStore FileStore()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Host:Storage:Endpoint"] = "https://account.eu.r2.cloudflarestorage.test",
                ["Host:Storage:Bucket"] = "starter-test",
                ["Host:Storage:AccessKeyId"] = "test-access-key",
                ["Host:Storage:SecretAccessKey"] = "test-secret-key",
            })
            .Build();
        var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(TimeProvider.System)
            .AddFileStorage()
            .BuildServiceProvider();

        // The client as the host configures it, with its HTTP calls answered here.
        ((AmazonS3Config)provider.GetRequiredService<IAmazonS3>().Config).HttpClientFactory = new RecordingClientFactory(_r2);
        return provider.GetRequiredService<IFileStore>();
    }

    private sealed record SentRequest(HttpMethod Method, string? ContentSha256, string? Trailer, IReadOnlyList<string> ContentEncoding, long? ContentLength);

    private sealed class RecordingClientFactory(RecordingHandler handler) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(handler, disposeHandler: false);
    }

    // Answers each call as R2 does when it accepts it. A listing holds one key the first time and none after, so a prefix's deletion
    // ends after one page.
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private bool _listed;

        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new SentRequest(
                request.Method,
                Header(request, "x-amz-content-sha256"),
                Header(request, "x-amz-trailer"),
                [.. request.Content?.Headers.ContentEncoding ?? []],
                request.Content?.Headers.ContentLength));
            if (request.Content is not null)
            {
                await request.Content.ReadAsByteArrayAsync(cancellationToken);
            }

            var listing = request.Method == HttpMethod.Get && request.RequestUri!.Query.Contains("list-type=2", StringComparison.Ordinal);
            return (request.Method.Method, listing) switch
            {
                ("GET", true) => Xml(ListingXml()),
                ("GET", false) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("zip"u8.ToArray()) },
                ("POST", _) => Xml("""<DeleteResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Deleted><Key>users/a/file</Key></Deleted></DeleteResult>"""),
                ("DELETE", _) => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => new HttpResponseMessage(HttpStatusCode.OK) { Headers = { ETag = new("\"etag\"") } },
            };
        }

        private static string? Header(HttpRequestMessage request, string name) =>
            request.Headers.TryGetValues(name, out var values) ? string.Join(',', values) : null;

        private static HttpResponseMessage Xml(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/xml") };

        private string ListingXml()
        {
            var contents = _listed ? string.Empty : "<Contents><Key>users/a/file</Key><Size>3</Size></Contents>";
            var count = _listed ? 0 : 1;
            _listed = true;
            return $"""<ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Name>starter-test</Name><Prefix>users/a/</Prefix><KeyCount>{count}</KeyCount><MaxKeys>1000</MaxKeys><IsTruncated>false</IsTruncated>{contents}</ListBucketResult>""";
        }
    }
}
