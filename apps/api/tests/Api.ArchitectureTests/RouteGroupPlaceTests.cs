namespace Api.ArchitectureTests;

// N5: the host defines every audience's route group in one file. Read from the source tree: under src, only that file calls
// MapGroup, so a group placed anywhere else fails here.
public sealed class RouteGroupPlaceTests
{
    private static readonly string RouteGroupsFile = Path.Combine("src", "Api", "Routing", "RouteGroups.cs");

    [Fact]
    public void PlaceRouteGroups_EverySourceFileUnderSrc_CallsMapGroupOnlyInRouteGroups()
    {
        var sourceFolder = Path.Combine(Solution.Root, "src") + Path.DirectorySeparatorChar;

        var callers = Solution.SolutionProjects
            .Where(project => Solution.FolderOf(project).StartsWith(sourceFolder, StringComparison.Ordinal))
            .SelectMany(Solution.SourceFilesOf)
            .Where(file => File.ReadAllText(file).Contains("MapGroup(", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(Solution.Root, file));

        callers.ShouldBe([RouteGroupsFile]);
    }
}
