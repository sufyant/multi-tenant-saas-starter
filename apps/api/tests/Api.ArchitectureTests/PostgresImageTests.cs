using System.Text.RegularExpressions;

namespace Api.ArchitectureTests;

// D1: the test fixtures and CI run one PostgreSQL image, pinned by digest in db/postgres-image. A fixture or a workflow that
// names an image of its own may run another 18.x than the image check.
public sealed partial class PostgresImageTests
{
    [Fact]
    public void PostgresImage_FixturesAndWorkflows_NameNoImageOfTheirOwn()
    {
        var fixtures = Directory.EnumerateFiles(Solution.Root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file) && !file.Contains($"{Path.DirectorySeparatorChar}spikes{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => ImageInCode().IsMatch(File.ReadAllText(file)));
        var workflows = Directory.EnumerateFiles(Path.Combine(Solution.Root, "..", "..", ".github", "workflows"), "*.yml")
            .Where(file => ImageInWorkflow().IsMatch(File.ReadAllText(file)));

        File.ReadAllText(Path.Combine(Solution.Root, "db", "postgres-image")).ShouldMatch(@"^postgres:18@sha256:[0-9a-f]{64}\n$");
        fixtures.Select(file => Path.GetRelativePath(Solution.Root, file)).ShouldBeEmpty();
        workflows.Select(Path.GetFileName).ShouldBeEmpty();
    }

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    [GeneratedRegex(@"PostgreSqlBuilder\(""")]
    private static partial Regex ImageInCode();

    [GeneratedRegex(@"\bpostgres:[0-9]")]
    private static partial Regex ImageInWorkflow();
}
