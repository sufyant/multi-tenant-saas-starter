using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

[assembly: AssemblyFixture(typeof(Api.IntegrationTests.Fixtures.ObjectStorage))]

namespace Api.IntegrationTests.Fixtures;

// One S3 server for the test assembly, SeaweedFS from the image that local runs use (apps/api/storage), with one bucket. Every host of the
// tests gets its settings (ApiFactory), as every process needs the file store. Each test keeps its files under its own user's or tenant's
// prefix, so tests share the bucket. A test reads the bucket only here, to check what the application left; the application never lists it.
public sealed class ObjectStorage : IAsyncLifetime
{
    public const string Bucket = "starter-test";

    // The identity of apps/api/storage/s3-identities.json: a local key with no secret value.
    private const string AccessKeyId = "local-access-key";

    private const string SecretAccessKey = "local-secret-key";

    private const int S3Port = 8333;

    private readonly IContainer _container = new ContainerBuilder(File.ReadAllText("seaweedfs-image").Trim())
        .WithResourceMapping(new FileInfo("s3-identities.json"), "/etc/seaweedfs/")
        .WithCommand("server", "-s3", "-s3.config=/etc/seaweedfs/s3-identities.json", "-master.volumeSizeLimitMB=64")
        .WithPortBinding(S3Port, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(S3Port).ForStatusCode(HttpStatusCode.Forbidden)))
        .Build();

    // The settings of every host of the tests, once the server runs.
    public static IReadOnlyDictionary<string, string?> Settings { get; private set; } = new Dictionary<string, string?>();

    private static AmazonS3Client? Client { get; set; }

    // The keys under a prefix, as the bucket holds them.
    public static async Task<IReadOnlyList<string>> KeysAsync(string prefix)
    {
        var page = await Client!.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Bucket, Prefix = prefix });
        return [.. (page.S3Objects ?? []).Select(file => file.Key).Order(StringComparer.Ordinal)];
    }

    // The content of a file, as the bucket holds it.
    public static async Task<byte[]> ReadAsync(string key)
    {
        using var file = await Client!.GetObjectAsync(Bucket, key);
        using var content = new MemoryStream();
        await file.ResponseStream.CopyToAsync(content);
        return content.ToArray();
    }

    // Puts a file into the bucket as another part of the system would, such as a tenant's file of a later module.
    public static Task PutAsync(string key, string content) =>
        Client!.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = key, ContentBody = content });

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        var endpoint = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(S3Port)}");
        Client = new AmazonS3Client(
            new BasicAWSCredentials(AccessKeyId, SecretAccessKey),
            new AmazonS3Config { ServiceURL = endpoint.ToString(), AuthenticationRegion = "auto", ForcePathStyle = true });
        await Client.PutBucketAsync(Bucket);
        Settings = new Dictionary<string, string?>
        {
            ["Host:Storage:Endpoint"] = endpoint.ToString(),
            ["Host:Storage:Bucket"] = Bucket,
            ["Host:Storage:AccessKeyId"] = AccessKeyId,
            ["Host:Storage:SecretAccessKey"] = SecretAccessKey,
        };
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        await _container.DisposeAsync();
    }
}
