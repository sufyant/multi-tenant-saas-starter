namespace Api.Storage;

// The object storage of the files (section 7, "File storage"): one private bucket per environment. The endpoint and the bucket come from
// .railway/railway.ts; the two keys are secrets, scoped to the bucket, that the maintainer enters in Railway.
internal sealed class StorageOptions
{
    public const string Section = "Host:Storage";

    /// <summary>The S3 API's address: R2's of the account's jurisdiction, or MinIO's in local runs and tests.</summary>
    public Uri? Endpoint { get; set; }

    public string? Bucket { get; set; }

    public string? AccessKeyId { get; set; }

    public string? SecretAccessKey { get; set; }

    /// <summary>The time limit of each call to the storage (Nygard, Chapter 5). The SDK retries a failed call within it.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
