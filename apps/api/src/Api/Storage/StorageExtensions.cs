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

        // R2 asks for the region "auto" and for addresses by path; MinIO accepts both. The SDK's own retries are bounded and within the time
        // limit of each call. R2 does not take the checksums the SDK adds by default, so the SDK sends and checks one only where the API
        // requires it (Cloudflare R2 documentation, "aws-sdk-net").
        services.AddSingleton<IAmazonS3>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
            return new AmazonS3Client(
                new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey),
                new AmazonS3Config
                {
                    ServiceURL = settings.Endpoint!.ToString(),
                    AuthenticationRegion = "auto",
                    ForcePathStyle = true,
                    Timeout = settings.Timeout,
                    MaxErrorRetry = 2,
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                });
        });
        services.AddSingleton<IFileStore, S3FileStore>();
        return services;
    }
}
