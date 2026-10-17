using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Api.ArchitectureTests;

// Section "Names and places": the names and folders the rules fix, read from the compiled code and the source tree.
public sealed partial class NamesAndPlacesTests
{
    private static readonly string[] CatchAllFolders = ["Helpers", "Utils", "Utilities", "Common", "Misc"];

    private static readonly string[] BuildOutput = ["bin", "obj"];

    public static TheoryData<string> ModuleDbContextNames => [.. ModuleDbContexts.Types().Select(type => type.FullName!)];

    // N4: a module's schema is its name in snake case, so the schema says which module owns a table.
    [Theory]
    [MemberData(nameof(ModuleDbContextNames))]
    public void NameSchemas_ModuleDbContext_UsesTheModuleNameInSnakeCase(string contextName)
    {
        var contextType = ModuleDbContexts.Types().Single(type => type.FullName == contextName);
        using var context = ModuleDbContexts.CreateAsDotnetEfDoes(contextType);
        var module = contextType.Assembly.GetName().Name!.Replace(".Infrastructure", "", StringComparison.Ordinal);

        var schema = context.Model.GetDefaultSchema();

        schema.ShouldBe(SnakeCase(module));
    }

    // N6: a permission is audience.resource.action. The audience is platform or tenant; the me audience has no permissions.
    [Fact]
    public void NamePermissions_EveryPermission_IsAudienceResourceAction()
    {
        var permissions = PermissionCatalogues()
            .SelectMany(catalogue => catalogue.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        var misnamed = permissions.Where(permission => !AudienceResourceAction().IsMatch(permission));

        permissions.ShouldNotBeEmpty();
        misnamed.ShouldBeEmpty();
    }

    // N7: the OpenAPI document builds its schema names from the type names, so two requests or responses of one name would clash.
    [Fact]
    public void NameApiTypes_RequestsAndResponses_AreUniqueAcrossTheApi()
    {
        var types = Solution.LayerOfEveryModule("Api").Append(Solution.Host)
            .SelectMany(project => Solution.Load(project).GetTypes())
            .Where(type => !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal) || type.Name.EndsWith("Response", StringComparison.Ordinal))
            .ToList();

        var clashes = types
            .GroupBy(type => type.Name)
            .Where(name => name.Count() > 1)
            .Select(name => $"{name.Key}: {string.Join(", ", name.Select(type => type.FullName))}");

        types.ShouldNotBeEmpty();
        clashes.ShouldBeEmpty();
    }

    // N8: a folder is named by what it holds. A catch-all name says nothing, so code piles up in it.
    [Fact]
    public void NameFolders_EveryProject_HasNoCatchAllFolder()
    {
        var catchAll = Solution.SolutionProjects
            .SelectMany(FoldersOf)
            .Where(folder => CatchAllFolders.Contains(Path.GetFileName(folder), StringComparer.OrdinalIgnoreCase))
            .Select(folder => Path.GetRelativePath(Solution.Root, folder));

        catchAll.ShouldBeEmpty();
    }

    // N10: a folder becomes a namespace, and a namespace hides a type of the same name (CS0118). A feature folder named after its
    // action would hide its command, so a feature folder takes the noun form of its root.
    [Fact]
    public void NameFolders_EveryProject_HasNoFolderNamedLikeATypeItSees()
    {
        var clashes = Solution.SolutionProjects
            .SelectMany(project => FoldersOf(project).Select(folder => (Project: project, Folder: Path.GetFileName(folder))))
            .Where(folder => TypeNamesSeenBy(folder.Project).Contains(folder.Folder))
            .Select(folder => $"{folder.Project}: {folder.Folder}");

        clashes.ShouldBeEmpty();
    }

    // N9: a test project is {Project}.{Kind}Tests, where Project is a production project or a module of the solution.
    [Fact]
    public void NameTestProjects_EveryTestProject_IsProjectDotKindTests()
    {
        var testProjects = Solution.SolutionProjects.Where(project => project.EndsWith("Tests", StringComparison.Ordinal)).ToList();

        var misnamed = testProjects.Where(project => !IsProjectDotKindTests(project));

        testProjects.ShouldNotBeEmpty();
        misnamed.ShouldBeEmpty();
    }

    private static IEnumerable<Type> PermissionCatalogues() =>
        Solution.LayerOfEveryModule("Contracts")
            .SelectMany(project => Solution.Load(project).GetTypes())
            .Where(type => type.Name == "Permissions");

    // Every folder under the project's own folder, without the build output.
    private static IEnumerable<string> FoldersOf(string project)
    {
        var root = Solution.FolderOf(project);
        return Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Where(folder => !Path.GetRelativePath(root, folder).Split(Path.DirectorySeparatorChar).Any(BuildOutput.Contains));
    }

    // The top-level types the project declares and the ones it takes from the projects it references directly.
    private static HashSet<string> TypeNamesSeenBy(string project) =>
    [
        .. Solution.ReadProjectFile(project).ProjectReferences.Append(project)
            .SelectMany(seen => Solution.Load(seen).GetTypes())
            .Where(type => !type.IsNested && !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
            .Select(type => type.Name),
    ];

    private static bool IsProjectDotKindTests(string project)
    {
        var match = ProjectDotKindTests().Match(project);
        var tested = match.Groups["project"].Value;

        return match.Success && (Solution.ProductionProjects.Contains(tested) || Solution.Modules.Contains(tested));
    }

    private static string SnakeCase(string name) => WordStart().Replace(name, "_$1").ToLowerInvariant();

    [GeneratedRegex(@"^(platform|tenant)\.[a-z]+(_[a-z]+)*\.[a-z]+(_[a-z]+)*$")]
    private static partial Regex AudienceResourceAction();

    [GeneratedRegex(@"^(?<project>.+)\.(Unit|Integration|EndToEnd|Architecture)Tests$")]
    private static partial Regex ProjectDotKindTests();

    [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
    private static partial Regex WordStart();
}
