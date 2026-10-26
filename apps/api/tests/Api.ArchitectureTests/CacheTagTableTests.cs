using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Tenancy;
using Tenancy.Caching;

namespace Api.ArchitectureTests;

// Section 3, "Cache": each module keeps in one place which entity types remove which tags (its tag table). A row names an entity of
// the module's model and a property of it. A cached read names only tags that a row of some tag table removes, or a missed removal would
// keep an old answer until it expires.
public sealed class CacheTagTableTests
{
    public static TheoryData<Type> ModuleContexts => [.. ModuleDbContexts.Types().Where(type => type.IsSubclassOf(typeof(TenantDbContext)))];

    [Theory]
    [MemberData(nameof(ModuleContexts))]
    public void TagTable_EveryRow_NamesAnEntityOfTheModelAndItsProperty(Type contextType)
    {
        using var context = ModuleDbContexts.CreateAsDotnetEfDoes(contextType);

        var unknown = TagTableOf(context)
            .Where(source => context.Model.FindEntityType(source.Entity) is not { } entity
                || (source.Property is not null && entity.FindProperty(source.Property) is null));

        unknown.ShouldBeEmpty();
    }

    [Fact]
    public void TagOfACachedRead_EveryRead_HasARowThatRemovesIt()
    {
        var removed = RemovedKinds();

        var unremoved = CachedReadsOf(Solution.AllProjects.Select(Solution.Load)).Where(read => read.TagKinds.Except(removed).Any());

        unremoved.Select(read => read.Kind).ShouldBeEmpty();
    }

    [Fact]
    public void TagOfACachedRead_ATagThatNoRowRemoves_IsFound()
    {
        var removed = RemovedKinds();

        var unremoved = CachedReadsOf([typeof(CacheTagTableTests).Assembly]).Where(read => read.TagKinds.Except(removed).Any());

        unremoved.Select(read => read.Kind).ShouldBe([PlantedCachedRead.Read.Kind]);
    }

    private static string[] RemovedKinds() =>
    [
        .. ModuleDbContexts.Types()
            .Where(type => type.IsSubclassOf(typeof(TenantDbContext)))
            .SelectMany(type =>
            {
                using var context = ModuleDbContexts.CreateAsDotnetEfDoes(type);
                return TagTableOf(context).Select(source => source.Kind).ToList();
            })
            .Distinct(),
    ];

    private static IReadOnlyList<CacheTagSource> TagTableOf(DbContext context) =>
        (IReadOnlyList<CacheTagSource>)typeof(TenantDbContext)
            .GetProperty("CacheTagSources", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(context)!;

    // Every cached read the code declares: a static field of a CachedRead type.
    private static IEnumerable<CachedRead> CachedReadsOf(IEnumerable<Assembly> assemblies) =>
        assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(field => field.FieldType.IsAssignableTo(typeof(CachedRead)))
            .Select(field => (CachedRead)field.GetValue(null)!);
}
