using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Api.Storage;

internal static class StorageExtensions
{
    // Every process needs the storage: the web makes download links, and the worker writes exports and deletes files. So a host without
    // its settings does not start, and names the missing key (section 7, "Configuration", rule 2).
    public static IServiceCollection AddFileStorage(this IServiceCollection services)
    {
        services.AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.Section)
            .Validate(settings => settings.Endpoint is { IsAbsoluteUri: true }, $"{StorageOptions.Section}:Endpoint must be the absolute URL of the S3 API.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Bucket), $"{StorageOptions.Section}:Bucket must name the bucket.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.AccessKeyId), $"{StorageOptions.Section}:AccessKeyId must be the key id of the bucket's token.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.SecretAccessKey), $"{StorageOptions.Section}:SecretAccessKey must be the secret of the bucket's token.")
            .Validate(settings => settings.Timeout > TimeSpan.Zero, $"{StorageOptions.Section}:Timeout must be positive.")
            .ValidateOnStart();

        // The settings follow R2 (Cloudflare R2 documentation, "aws-sdk-net" and "S3 API compatibility"); SeaweedFS accepts each of them.
        // S3FileStoreRequestTests pins the requests they give. An upload also signs its body in one part (S3FileStore.SaveAsync).
        services.AddSingleton<IAmazonS3>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
            return new AmazonS3Client(
                new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey),
                new AmazonS3Config
                {
                    ServiceURL = settings.Endpoint!.ToString(),

                    // R2 has one region, "auto", and signs with it.
                    AuthenticationRegion = "auto",

                    // The bucket goes into the path, not into the host name, so the endpoint of the account serves every bucket.
                    ForcePathStyle = true,

                    // The SDK's own retries are bounded and within the time limit of each call (section 7, "File storage", rule 1).
                    Timeout = settings.Timeout,
                    MaxErrorRetry = 2,

                    // R2 does not take the checksums that the SDK adds by default, which an upload sends in a trailer. So the SDK sends and
                    // checks one only where the S3 API requires it, such as the deletion of many keys.
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                });
        });
        services.AddSingleton<IFileStore, S3FileStore>();
        return services;
    }
}
