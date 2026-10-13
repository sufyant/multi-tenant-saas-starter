using System.Text.Json;
using System.Xml.Linq;

namespace Api.ArchitectureTests;

// Fixed rule 4: no commercially licensed package. docs/licenses.md lists every package of every project in the solution, direct and
// transitive, once by name, with the license the package declares. A license can change at a new version, so every version in use
// must declare the license of its row. A package that is not listed, or whose license is not permitted, fails here before it
// reaches a release.
public sealed class LicenseTests
{
    private const string LicensesFile = "docs/licenses.md";

    // Permissive licenses. Any other, commercial and source-available ones above all, needs a decision first.
    private static readonly string[] Permitted = ["MIT", "Apache-2.0", "BSD-3-Clause", "PostgreSQL"];

    [Fact]
    public void ListPackages_EveryProjectOfTheSolution_HasExactlyOneRowPerPackage()
    {
        var used = UsedPackages().Select(package => package.Package).Distinct();

        var listed = Listed().Select(row => row.Package);

        listed.ShouldBe(used, ignoreOrder: true);
    }

    [Fact]
    public void ReadLicenses_EveryVersionOfEveryPackageInUse_DeclaresTheLicenseOfItsRow()
    {
        var listed = Listed();

        // A package whose versions declare any license other than the one of its row is named with each of its versions,
        // so two versions that declare different licenses are both named.
        var differing = Solution.SolutionProjects.SelectMany(DeclaredLicensesOf).Distinct()
            .GroupBy(package => package.Package)
            .Where(versions => !versions.Select(version => version.License).Distinct()
                .SequenceEqual(listed.Where(row => row.Package == versions.Key).Select(row => row.License)))
            .SelectMany(versions => versions);

        differing.ShouldBeEmpty();
    }

    [Fact]
    public void ReadLicenses_EveryPackageInUse_IsPermittedAndNotNamedByFixedRule4()
    {
        var listed = Listed();
        var used = UsedPackages();

        listed.Where(row => !Permitted.Contains(row.License)).ShouldBeEmpty();
        used.Where(package => IsNamedByFixedRule4(package.Package, package.Version)).ShouldBeEmpty();
    }

    // The rows of the table in the file, after its header and separator: package and license.
    private static List<(string Package, string License)> Listed() =>
    [
        .. File.ReadLines(Path.Combine(Solution.Root, LicensesFile))
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .Skip(2)
            .Select(line => line.Split('|', StringSplitOptions.TrimEntries))
            .Select(cells => (cells[1], cells[2])),
    ];

    // Every package and version that NuGet restored for any project of the solution.
    private static List<(string Package, string Version)> UsedPackages() =>
        [.. Solution.SolutionProjects.SelectMany(PackagesOf).Distinct()];

    private static IEnumerable<(string Package, string Version)> PackagesOf(string project)
    {
        using var assets = JsonDocument.Parse(File.ReadAllText(Solution.AssetsFileOf(project)));

        return
        [
            .. assets.RootElement.GetProperty("libraries").EnumerateObject()
                .Where(library => library.Value.GetProperty("type").GetString() == "package")
                .Select(library => library.Name.Split('/'))
                .Select(nameAndVersion => (nameAndVersion[0], nameAndVersion[1])),
        ];
    }

    // The license expression in each package's own manifest, in the folder NuGet restored it to.
    private static IEnumerable<(string Package, string Version, string License)> DeclaredLicensesOf(string project)
    {
        using var assets = JsonDocument.Parse(File.ReadAllText(Solution.AssetsFileOf(project)));
        var folders = assets.RootElement.GetProperty("packageFolders").EnumerateObject().Select(folder => folder.Name).ToList();

        return
        [
            .. PackagesOf(project).Select(package => (package.Package, package.Version, LicenseIn(ManifestOf(package.Package, package.Version, folders)))),
        ];
    }

    private static string ManifestOf(string package, string version, IEnumerable<string> folders) =>
        folders
            .Select(folder => Path.Combine(folder, package.ToLowerInvariant(), version.ToLowerInvariant(), $"{package.ToLowerInvariant()}.nuspec"))
            .First(File.Exists);

    // A package without a license expression (a license file or a URL only) reads as such, which no permitted license matches.
    private static string LicenseIn(string manifest) =>
        XDocument.Load(manifest).Descendants().FirstOrDefault(element => element.Name.LocalName == "license" && (string?)element.Attribute("type") == "expression")?.Value.Trim()
        ?? $"no license expression in {Path.GetFileName(manifest)}";

    // MediatR, AutoMapper, FluentAssertions, and MassTransit from version 9.
    private static bool IsNamedByFixedRule4(string package, string version)
    {
        string[] anyVersion = ["MediatR", "AutoMapper", "FluentAssertions"];
        if (anyVersion.Any(name => package == name || package.StartsWith($"{name}.", StringComparison.Ordinal)))
        {
            return true;
        }

        return (package == "MassTransit" || package.StartsWith("MassTransit.", StringComparison.Ordinal))
            && int.Parse(version.Split('.')[0], System.Globalization.CultureInfo.InvariantCulture) >= 9;
    }
}
