using System.Reflection;
using NetArchTest.Rules;
using Tenancy.Caching;

namespace Api.ArchitectureTests;

// Section 3, "Cache": ReadCache is the one way to cache a read, and the composition root is the one place that chooses the cache. No
// other code calls HybridCache, FusionCache, Valkey's client or a distributed cache, and no command handler caches a read. Each rule is
// also checked against a type of this assembly that breaks it (Fixtures/Planted...), so the check is known to find a breach.
public sealed class CachingTests
{
    private static readonly string[] CacheLibraries =
    [
        "Microsoft.Extensions.Caching.Hybrid",
        "Microsoft.Extensions.Caching.Distributed",
        "Microsoft.Extensions.Caching.StackExchangeRedis",
        "StackExchange.Redis",
        "ZiggyCreatures.Caching.Fusion",
    ];

    private static readonly string[] AllowedToCallTheCache = ["Tenancy.Caching.ReadCache", "Api.Caching.CachingExtensions"];

    public static TheoryData<string> ProductionProjects => [.. Solution.AllProjects];

    [Theory]
    [MemberData(nameof(ProductionProjects))]
    public void CallTheCache_AnyCodeButReadCacheAndTheCompositionRoot_IsRefused(string project)
    {
        var callers = TypesCalling(Solution.Load(project), CacheLibraries);

        callers.Except(AllowedToCallTheCache).ShouldBeEmpty();
    }

    [Fact]
    public void CallTheCache_ATypeOutsideTheTwo_IsFound()
    {
        var callers = TypesCalling(typeof(CachingTests).Assembly, CacheLibraries);

        callers.ShouldContain(typeof(PlantedCacheCaller).FullName!);
    }

    [Theory]
    [MemberData(nameof(ProductionProjects))]
    public void CacheARead_InACommandHandler_IsRefused(string project)
    {
        var handlers = CommandHandlersCalling(Solution.Load(project), [typeof(ReadCache).FullName!]);

        handlers.ShouldBeEmpty();
    }

    [Fact]
    public void CacheARead_APlantedCommandHandler_IsFound()
    {
        var handlers = CommandHandlersCalling(typeof(CachingTests).Assembly, [typeof(ReadCache).FullName!]);

        handlers.ShouldBe([typeof(PlantedCommandHandler).FullName!]);
    }

    // The types that use any of the namespaces or types, each named by its outermost type, so a lambda or a state machine counts for
    // the type that holds it.
    private static IEnumerable<string> TypesCalling(Assembly assembly, string[] dependencies) =>
        Types.InAssembly(assembly).That().HaveDependencyOnAny(dependencies).GetTypes()
            .Select(type => OutermostName(type.ReflectionType))
            .Distinct();

    // A command handler handles a message named ...Command (N8). Its type, or a type nested in it, uses the dependency.
    private static IEnumerable<string> CommandHandlersCalling(Assembly assembly, string[] dependencies) =>
        TypesCalling(assembly, dependencies)
            .Select(name => assembly.GetType(name)!)
            .Where(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Any(method => method.Name.StartsWith("Handle", StringComparison.Ordinal)
                    && method.GetParameters().FirstOrDefault()?.ParameterType.Name.EndsWith("Command", StringComparison.Ordinal) == true))
            .Select(type => type.FullName!);

    private static string OutermostName(Type type) => type.DeclaringType is { } declaring ? OutermostName(declaring) : type.FullName!;
}
