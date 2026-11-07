namespace SharedKernel;

/// <summary>
/// The one form of the keys of <see cref="IFileStore"/>: ids only, never a name or an address, because a key reaches the logs. A user's
/// files are under <c>users/{userId}/</c>, and their data exports under <c>exports/users/{userId}/</c>, because the bucket's lifecycle rule,
/// the second guard of an export's deletion, matches a prefix only. A tenant's files are under <c>tenants/{tenantId}/</c>. So a deletion
/// deletes whole prefixes (section 7, "File storage"). A tenant's file will be <c>tenants/{tenantId}/{module}/{fileId}</c>; no module keeps
/// one yet. The host's readiness check writes its file under <c>health/</c>.
/// </summary>
public static class FileKeys
{
    /// <summary>The prefix of the files of the storage's readiness check, which the bucket's lifecycle rule deletes after a day.</summary>
    public const string Health = "health/";

    /// <summary>The prefix of every export, which the bucket's lifecycle rule deletes after 7 days.</summary>
    public const string Exports = "exports/";

    /// <summary>The prefix of every file of the user, besides their exports.</summary>
    public static string User(Guid userId) => $"users/{userId}/";

    /// <summary>The prefix of every data export of the user.</summary>
    public static string UserExports(Guid userId) => $"{Exports}users/{userId}/";

    /// <summary>Every prefix that holds a file of the user, which the user's deletion deletes.</summary>
    public static IReadOnlyList<string> AllOfUser(Guid userId) => [User(userId), UserExports(userId)];

    /// <summary>The prefix of every file of the tenant.</summary>
    public static string Tenant(Guid tenantId) => $"tenants/{tenantId}/";

    /// <summary>The file of one data export of the user.</summary>
    public static string UserExport(Guid userId, Guid exportId) => $"{UserExports(userId)}{exportId}.zip";

    /// <summary>The file of one run of the storage's readiness check.</summary>
    public static string HealthCheck(Guid checkId) => $"{Health}{checkId}";
}
