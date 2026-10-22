using System.Text.RegularExpressions;

namespace Api.ArchitectureTests;

// Every rule id that a comment in code, tests or scripts cites is defined in a document (ARCHITECTURE.md: "Use the id when you refer to
// a rule"). A document defines an id in the first cell of a table row (`| W13 |`) or in a heading (`### K2. Invitations`). An id that
// no document defines, such as one from a review's own list, points a reader to nothing.
public sealed partial class RuleIdTests
{
    private static readonly string Repository = Path.GetFullPath(Path.Combine(Solution.Root, "..", ".."));

    // The documents that define rule ids.
    private static readonly string[] Documents =
    [
        Path.Combine(Solution.Root, "docs", "ARCHITECTURE.md"),
        Path.Combine(Solution.Root, "docs", "api-catalog.md"),
        Path.Combine(Solution.Root, "docs", "configuration.md"),
        Path.Combine(Repository, "docs", "frontend", "ARCHITECTURE.md"),
    ];

    // The code, tests and scripts of the repository. Generated files and build output cite nothing of their own.
    private static readonly string[] Folders = ["apps", "packages", "scripts", ".github", ".railway"];

    private static readonly HashSet<string> Extensions = [".cs", ".ts", ".tsx", ".js", ".mjs", ".cjs", ".sh", ".yml", ".yaml"];

    private static readonly HashSet<string> SkippedFolders =
        ["bin", "obj", "node_modules", ".next", ".turbo", "dist", "coverage", "generated", "Generated", "Migrations", "TestResults"];

    [Fact]
    public void CiteRule_InCodeTestsOrScripts_IsDefinedInADocument()
    {
        var defined = Documents.SelectMany(File.ReadLines).SelectMany(line => DefinedId().Matches(line)).Select(match => match.Groups["id"].Value).ToHashSet();

        var undefined = CodeFiles()
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, number: index + 1, comment: CommentOf(file, line))))
            .SelectMany(line => RuleId().Matches(line.comment).Select(match => (line.file, line.number, id: match.Value)))
            .Where(citation => !defined.Contains(citation.id))
            .Select(citation => $"{Path.GetRelativePath(Repository, citation.file)}:{citation.number}: {citation.id}");

        defined.ShouldContain("W13");
        undefined.ShouldBeEmpty();
    }

    private static IEnumerable<string> CodeFiles() =>
        Folders.Select(folder => Path.Combine(Repository, folder)).Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            .Where(file => Extensions.Contains(Path.GetExtension(file)))
            .Where(file => !Path.GetFileName(file).Contains(".g.", StringComparison.Ordinal) && !Path.GetFileName(file).Contains(".gen.", StringComparison.Ordinal))
            .Where(file => !Path.GetRelativePath(Repository, file).Split(Path.DirectorySeparatorChar).Any(SkippedFolders.Contains));

    // Only a comment cites a rule; code may hold such a word for another reason, such as a route constant or a number format.
    private static string CommentOf(string file, string line)
    {
        var comment = Path.GetExtension(file) is ".sh" or ".yml" or ".yaml" ? HashComment().Match(line) : SlashComment().Match(line);
        return comment.Success ? comment.Groups["text"].Value : "";
    }

    // A rule id is one or two capital letters, one or two digits and an optional lowercase letter for a part: R4, W13, N7a, N11w, K6a, F43.
    // No letter, digit, underscore, dot or hyphen touches it on either side. So it does not catch SHA256 or API4 (three letters),
    // CA1716 or IDE0130 (more digits), E2E or UTF8 (a letter around the digits), CC0-1.0 (a hyphen), a spike test's name such as
    // T10a_failed_result (an underscore), or HTTP 404 and RFC 9110 (no digits next to the letters).
    [GeneratedRegex(@"(?<![\w.\-])[A-Z]{1,2}[0-9]{1,2}[a-z]?(?![\w\-])")]
    private static partial Regex RuleId();

    [GeneratedRegex(@"^(?:\|\s*(?<id>[A-Z]{1,2}[0-9]{1,2}[a-z]?)\s*\||#{2,}\s+(?<id>[A-Z]{1,2}[0-9]{1,2}[a-z]?)\.\s)")]
    private static partial Regex DefinedId();

    // A comment in C#, TypeScript and JavaScript: after // or /*, or a line of a block comment that starts with *.
    [GeneratedRegex(@"(?://|/\*)(?<text>.*)$|^\s*\*(?<text>.*)$")]
    private static partial Regex SlashComment();

    // A comment in a shell script or a workflow: after a # at the start of the line or after a space.
    [GeneratedRegex(@"(?:^|\s)#(?<text>.*)$")]
    private static partial Regex HashComment();
}
