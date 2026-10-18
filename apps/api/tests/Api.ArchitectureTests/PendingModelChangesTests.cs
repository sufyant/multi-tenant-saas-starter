using Microsoft.EntityFrameworkCore;

namespace Api.ArchitectureTests;

// A model change without a migration leaves the database behind the code, and most tests run on a database built from the
// migrations, so they do not notice. HasPendingModelChanges compares the model with the last migration's snapshot; it never
// connects. Every concrete DbContext of every module is checked, so a new module's DbContext is covered without a change here.
public sealed class PendingModelChangesTests
{
    public static TheoryData<string> ModuleDbContextNames => [.. ModuleDbContexts.Types().Select(type => type.FullName!)];

    [Theory]
    [MemberData(nameof(ModuleDbContextNames))]
    public void CompareModel_ModuleDbContext_HasNoChangesWithoutAMigration(string contextName)
    {
        var contextType = ModuleDbContexts.Types().Single(type => type.FullName == contextName);
        using var context = ModuleDbContexts.CreateAsDotnetEfDoes(contextType);

        var pending = context.Database.HasPendingModelChanges();

        pending.ShouldBeFalse(
            $"{contextType.Name} has model changes without a migration. "
            + $"Run: dotnet tool restore, then dotnet ef migrations add <Name> --project {ProjectPathOf(contextType)}");
    }

    private static string ProjectPathOf(Type contextType) =>
        Path.GetRelativePath(Solution.Root, Solution.FolderOf(contextType.Assembly.GetName().Name!)).Replace('\\', '/');
}
