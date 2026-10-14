using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.ArchitectureTests.Fixtures;

// Every concrete DbContext of every module, built the way `dotnet ef` builds it: through the design-time factory in the context's
// assembly, without the host and without a database connection.
internal static class ModuleDbContexts
{
    public static IEnumerable<Type> Types() =>
        Solution.ModuleProjects
            .Append(Solution.Host)
            .SelectMany(project => Solution.Load(project).GetTypes())
            .Where(type => type.IsSubclassOf(typeof(DbContext)) && !type.IsAbstract);

    public static DbContext CreateAsDotnetEfDoes(Type contextType)
    {
        var factoryInterface = typeof(IDesignTimeDbContextFactory<>).MakeGenericType(contextType);
        var factory = contextType.Assembly.GetTypes().SingleOrDefault(type => type.IsAssignableTo(factoryInterface) && !type.IsAbstract)
            ?? throw new InvalidOperationException(
                $"{contextType.Name} has no {nameof(IDesignTimeDbContextFactory<>)} in its assembly, so `dotnet ef` cannot build it.");

        return (DbContext)factoryInterface.GetMethod(nameof(IDesignTimeDbContextFactory<>.CreateDbContext))!
            .Invoke(Activator.CreateInstance(factory), [Array.Empty<string>()])!;
    }
}
