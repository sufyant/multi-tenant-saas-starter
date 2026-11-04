namespace SharedKernel;

/// <summary>
/// The one form of the keys of <see cref="IFileStore"/>: ids only, never a name or an address, because a key reaches the logs. A user's
/// files are under <c>users/{userId}/</c> and a tenant's under <c>tenants/{tenantId}/</c>, so a deletion of either deletes one prefix
/// (section 7, "File storage"). A tenant's file will be <c>tenants/{tenantId}/{module}/{fileId}</c>; no module keeps one yet.
/// </summary>
public static class FileKeys
{
    /// <summary>The prefix of every file of the user.</summary>
    public static string User(Guid userId) => $"users/{userId}/";

    /// <summary>The prefix of every file of the tenant.</summary>
    public static string Tenant(Guid tenantId) => $"tenants/{tenantId}/";

    /// <summary>The file of one data export of the user.</summary>
    public static string UserExport(Guid userId, Guid exportId) => $"{User(userId)}exports/{exportId}.zip";
}
