using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Api.ArchitectureTests;

// R1: TenantDbContext indexes the tenant column of every tenant table, unless a key or another index leads with it already. Such a one
// serves every read inside the tenant, so an index of the tenant column alone beside it only costs each write (PostgreSQL documentation,
// 11.3 "Multicolumn Indexes"). A partial index serves only its own rows, so it does not count.
public sealed class TenantIndexTests
{
    private const string TenantColumn = "TenantId";

    public static TheoryData<string> ModuleDbContextNames => [.. ModuleDbContexts.Types().Select(type => type.FullName!)];

    [Theory]
    [MemberData(nameof(ModuleDbContextNames))]
    public void IndexTenantColumn_ModuleDbContext_AddsNoIndexThatAKeyOrAnotherIndexCovers(string contextName)
    {
        var contextType = ModuleDbContexts.Types().Single(type => type.FullName == contextName);
        using var context = ModuleDbContexts.CreateAsDotnetEfDoes(contextType);

        var covered = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetIndexes()
                .Where(index => index.Properties is [{ Name: TenantColumn }])
                .Where(index => LeadsWithTheTenant(entity).Any(properties => !ReferenceEquals(properties, index.Properties)))
                .Select(index => index.GetDatabaseName()));

        covered.ShouldBeEmpty();
    }

    // The column lists of the entity's keys and whole indexes that lead with the tenant column.
    private static IEnumerable<IReadOnlyList<IProperty>> LeadsWithTheTenant(IEntityType entity) =>
        entity.GetKeys().Select(key => key.Properties)
            .Concat(entity.GetIndexes().Where(index => index.GetFilter() is null).Select(index => index.Properties))
            .Where(properties => properties[0].Name == TenantColumn);
}
