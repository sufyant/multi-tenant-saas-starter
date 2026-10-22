using System.Reflection;

namespace Api.ArchitectureTests;

// K6a: a repository method that returns an aggregate root locks it, and its name says so. A query class returns no root (Q2), so a
// handler gets a root only from a repository (Q1), and it cannot change a root it did not lock. A part of an aggregate is not checked:
// the handler locked its root first. Audit's IAuditLog is neither, and returns its entries without a lock, because an entry never
// changes (K3).
public sealed class RepositoryLockTests
{
    private static readonly string[] LockSuffixes = ["ForUpdateAsync", "ForNoKeyUpdateAsync"];

    // K6a: the one read of a root that no command changes, which a repository returns without a lock, wrapped in a type of its own: the
    // tenant's roles (TenantRoles, A2), which ITenantRepository returns, and MemberFacts carries.
    private static readonly Dictionary<string, string[]> WrappedRootReads = new(StringComparer.Ordinal)
    {
        ["ControlPlane"] = ["ITenantRepository.FindMemberFactsAsync", "ITenantRepository.FindRolesAsync"],
    };

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

    // A type that wraps a root hides it from the test above. Only the reads that K6a names may return one.
    [Theory]
    [MemberData(nameof(Modules))]
    public void LoadAggregateRoot_WrappedInAReturnedType_IsOnlyAReadThatK6aNames(string module)
    {
        var roots = AggregateRootsOf(module);

        var wrapping = RepositoriesOf(module)
            .SelectMany(repository => repository.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => !Mentions(method.ReturnType, roots) && Wraps(method.ReturnType, roots, module, []))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}");

        wrapping.ShouldBe(WrappedRootReads.GetValueOrDefault(module, []), ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void LoadAggregateRoot_FromAQueryClass_FindsNone(string module)
    {
        var roots = AggregateRootsOf(module);

        var returningRoots = InterfacesOf(module, "Queries")
            .SelectMany(queries => queries.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => Mentions(method.ReturnType, roots))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}");

        returningRoots.ShouldBeEmpty();
    }

    // The tests above find the roots by their folder. Each repository, I{Aggregate}Repository (Q1), names one of them, so a root that the
    // tests above do not find fails here.
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

    private static IEnumerable<Type> RepositoriesOf(string module) => InterfacesOf(module, "Repository");

    // I{Aggregate}Repository and I{Resource}Queries (Q1, Q2), by the role their name ends with (N8).
    private static IEnumerable<Type> InterfacesOf(string module, string role) =>
        Solution.Load($"{module}.Application").GetTypes()
            .Where(type => type.IsInterface && type.Name.StartsWith('I') && type.Name.EndsWith(role, StringComparison.Ordinal));

    private static string PluralOf(string name) => name.EndsWith('y') ? $"{name[..^1]}ies" : $"{name}s";

    // Whether a type of the module holds a root in a property or a constructor parameter, at any depth: Task<MemberFacts> holds
    // TenantRoles, which holds a list of Role.
    private static bool Wraps(Type type, HashSet<Type> roots, string module, HashSet<Type> seen) =>
        seen.Add(type)
        && ((type.HasElementType && Wraps(type.GetElementType()!, roots, module, seen))
            || type.GenericTypeArguments.Any(argument => Wraps(argument, roots, module, seen))
            || (type.Assembly.GetName().Name!.StartsWith($"{module}.", StringComparison.Ordinal) && !roots.Contains(type)
                && type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(property => property.PropertyType)
                    .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SelectMany(constructor => constructor.GetParameters()).Select(parameter => parameter.ParameterType))
                    .Any(held => Mentions(held, roots) || Wraps(held, roots, module, seen))));

    // The type itself, or a type it wraps: Task<T>, a list of T, T[].
    private static bool Mentions(Type type, HashSet<Type> roots) =>
        roots.Contains(type)
        || (type.HasElementType && Mentions(type.GetElementType()!, roots))
        || type.GenericTypeArguments.Any(argument => Mentions(argument, roots));
}
