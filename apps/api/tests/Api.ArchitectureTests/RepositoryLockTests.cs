using System.Reflection;

namespace Api.ArchitectureTests;

// K6a: a repository method that returns an aggregate root locks it, and its name says so. A handler gets a root only from a repository
// (Q1, Q2), so a handler cannot change a root it did not lock. A part of an aggregate is not checked: the handler locked its root first.
public sealed class RepositoryLockTests
{
    private static readonly string[] LockSuffixes = ["ForUpdateAsync", "ForNoKeyUpdateAsync"];

    public static TheoryData<string> Modules => [.. Solution.Modules];

    [Theory]
    [MemberData(nameof(Modules))]
    public void LoadAggregateRoot_FromARepository_TakesItsLock(string module)
    {
        var roots = AggregateRootsOf(module);

        var unlocked = RepositoriesOf(module)
            .SelectMany(repository => repository.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => Mentions(method.ReturnType, roots))
            .Where(method => !LockSuffixes.Any(suffix => method.Name.EndsWith(suffix, StringComparison.Ordinal)))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}");

        unlocked.ShouldBeEmpty();
    }

    // The test above finds the roots by their folder. Each repository, I{Aggregate}Repository (Q1), names one of them, so a root that the
    // test above does not find fails here.
    [Theory]
    [MemberData(nameof(Modules))]
    public void FindAggregateRoot_OfEachRepository_IsFound(string module)
    {
        var roots = AggregateRootsOf(module).Select(root => root.Name).ToHashSet();

        var unknown = RepositoriesOf(module)
            .Where(repository => !roots.Contains(repository.Name["I".Length..^"Repository".Length]))
            .Select(repository => repository.Name);

        unknown.ShouldBeEmpty();
    }

    // N8: a Domain type's folder is its aggregate, and the type with the folder's singular name is the root (Tenants/Tenant.cs).
    private static HashSet<Type> AggregateRootsOf(string module) =>
        [.. Solution.Load($"{module}.Domain").GetTypes().Where(type => type.Namespace == $"{module}.Domain.{PluralOf(type.Name)}")];

    private static IEnumerable<Type> RepositoriesOf(string module) =>
        Solution.Load($"{module}.Application").GetTypes()
            .Where(type => type.IsInterface && type.Name.StartsWith('I') && type.Name.EndsWith("Repository", StringComparison.Ordinal));

    private static string PluralOf(string name) => name.EndsWith('y') ? $"{name[..^1]}ies" : $"{name}s";

    // The type itself, or a type it wraps: Task<T>, a list of T, T[].
    private static bool Mentions(Type type, HashSet<Type> roots) =>
        roots.Contains(type)
        || (type.HasElementType && Mentions(type.GetElementType()!, roots))
        || type.GenericTypeArguments.Any(argument => Mentions(argument, roots));
}
