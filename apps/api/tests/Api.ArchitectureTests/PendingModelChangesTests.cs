using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.ArchitectureTests;

// A model change without a migration leaves the database behind the code, and most tests run on a database built from the
// migrations, so they do not notice. HasPendingModelChanges compares the model with the last migration's snapshot; it never
// connects. Every concrete DbContext of every module is checked, so a new module's DbContext is covered without a change here.
public class PendingModelChangesTests
{
    public static TheoryData<string> ModuleDbContexts => [.. ModuleDbContextTypes().Select(type => type.FullName!)];

    [Theory]
    [MemberData(nameof(ModuleDbContexts))]
    public void CompareModel_ModuleDbContext_HasNoChangesWithoutAMigration(string contextName)
    {
        var contextType = ModuleDbContextTypes().Single(type => type.FullName == contextName);
        using var context = CreateAsDotnetEfDoes(contextType);

        var pending = context.Database.HasPendingModelChanges();

        pending.ShouldBeFalse(
            $"{contextType.Name} has model changes without a migration. "
            + $"Run: dotnet tool restore, then dotnet ef migrations add <Name> --project {ProjectPathOf(contextType)}");
    }

    private static IEnumerable<Type> ModuleDbContextTypes() =>
        Solution.ModuleProjects
            .SelectMany(project => Solution.Load(project).GetTypes())
            .Where(type => type.IsSubclassOf(typeof(DbContext)) && !type.IsAbstract);

    // `dotnet ef` builds a context through the design-time factory in the context's assembly, without the host and without a
    // database connection.
    private static DbContext CreateAsDotnetEfDoes(Type contextType)
    {
        var factoryInterface = typeof(IDesignTimeDbContextFactory<>).MakeGenericType(contextType);
        var factory = contextType.Assembly.GetTypes().SingleOrDefault(type => type.IsAssignableTo(factoryInterface) && !type.IsAbstract)
            ?? throw new InvalidOperationException(
                $"{contextType.Name} has no {nameof(IDesignTimeDbContextFactory<>)} in its assembly, so `dotnet ef` cannot build it.");

        return (DbContext)factoryInterface.GetMethod(nameof(IDesignTimeDbContextFactory<>.CreateDbContext))!
            .Invoke(Activator.CreateInstance(factory), [Array.Empty<string>()])!;
    }

    private static string ProjectPathOf(Type contextType) =>
        Path.GetRelativePath(Solution.Root, Solution.FolderOf(contextType.Assembly.GetName().Name!)).Replace('\\', '/');
}
