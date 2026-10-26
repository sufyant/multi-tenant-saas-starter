namespace Api.ArchitectureTests;

// Section 3, "Cache": a write that the change tracker does not see, a bulk statement or raw SQL, removes the tags of the cached rows it
// changes, in the same method, with TenantDbContext.RemoveCacheTagsAfterCommit. A forgotten removal is a red test, not a stale answer.
// The check reads the source; the theory below shows it finds each kind of breach.
public sealed class BulkWriteTests
{
    private const string Header =
        """
        using ControlPlane.Infrastructure.Persistence;
        using Microsoft.EntityFrameworkCore;
        using Tenancy.Caching;

        namespace Planted;

        internal sealed class PlantedWrites(ControlPlaneDbContext controlPlane)
        {
        """;

    public static TheoryData<string, int> PlantedWrites => new()
    {
        { """public Task Run(Guid id) => controlPlane.Database.ExecuteSqlAsync($"UPDATE control_plane.memberships SET role_id = {id}");""", 1 },
        { """public Task Run() => controlPlane.TenantRoles.Where(role => role.BuiltIn == null).ExecuteDeleteAsync();""", 1 },
        { """public Task Run(Guid id) => controlPlane.Database.ExecuteSqlAsync($"UPDATE control_plane.users SET suspended_at = now() WHERE id = {id}");""", 1 },
        { """public Task Run(Guid id) => controlPlane.Database.ExecuteSqlAsync($"UPDATE control_plane.users SET membership_count = membership_count + 1 WHERE id = {id}");""", 0 },
        { """public Task Run(Guid id) => controlPlane.Database.ExecuteSqlAsync($"UPDATE control_plane.invitations SET status = 'Cancelled' WHERE id = {id}");""", 0 },
        { """public Task Run(Guid id) => controlPlane.Database.ExecuteSqlAsync($"SELECT * FROM control_plane.tenants WHERE id = {id} FOR UPDATE");""", 0 },
        {
            """
            public async Task Run(Guid id)
            {
                await controlPlane.TenantRoles.ExecuteDeleteAsync();
                controlPlane.RemoveCacheTagsAfterCommit(CacheTag.Of(CacheTags.Tenant, id));
            }
            """,
            0
        },
        {
            """
            public async Task Run(Guid id)
            {
                await controlPlane.TenantRoles.ExecuteDeleteAsync();
                controlPlane.RemoveCacheTagsAfterCommit(CacheTag.Of(CacheTags.User, id));
            }
            """,
            1
        },
    };

    [Fact]
    public void WriteInBulk_EveryWriteToACachedTable_RemovesItsTags()
    {
        var tables = BulkWrites.CachedTables();

        var findings = Solution.ProductionProjects
            .SelectMany(Solution.SourceFilesOf)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => BulkWrites.WithoutRemoval(Path.GetRelativePath(Solution.Root, file), File.ReadAllText(file), tables));

        tables.ShouldNotBeEmpty();
        findings.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(PlantedWrites))]
    public void WriteInBulk_APlantedWrite_IsFoundUnlessItRemovesItsTags(string method, int findings)
    {
        var tables = BulkWrites.CachedTables();

        var found = BulkWrites.WithoutRemoval("Planted.cs", $"{Header}\n{method}\n}}", tables);

        found.Count().ShouldBe(findings);
    }
}
