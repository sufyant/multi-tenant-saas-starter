using System.Reflection;
using System.Text.RegularExpressions;
using Audit.Domain;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Solution = Api.ArchitectureTests.Fixtures.Solution;

namespace Api.ArchitectureTests;

// K3: AuditOperations declares the operation of every audit entry once. Each Audit handler names its operation from it, and the
// catalog's Audit column, the specification, lists exactly its operations. OpenApiContractTests checks the enum in the document.
public partial class AuditOperationTests
{
    private const string Audit = "Audit.Application";

    private static readonly string[] Records = ["AuditEntry", "PlatformAuditEntry"];

    [Fact]
    public void WriteAuditEntry_EveryHandler_NamesItsOperationFromAuditOperations()
    {
        var operations = OperationArgumentsOfEveryRecord();

        var undeclared = operations.Where(operation => !operation.Argument.StartsWith($"{nameof(AuditOperations)}.", StringComparison.Ordinal));

        operations.ShouldNotBeEmpty();
        undeclared.Select(operation => $"{operation.File}: {operation.Argument}").ShouldBeEmpty(
            "An audit handler names its operation as a constant of AuditOperations (K3).");
    }

    [Fact]
    public void DocumentAuditOperations_Catalog_ListsEveryOperationOfAuditOperations()
    {
        var documented = OperationsInTheCatalog();

        documented.ShouldBe(DeclaredOperations());
    }

    private static List<string> DeclaredOperations() =>
    [
        .. typeof(AuditOperations).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal),
    ];

    // The operation argument of each record an Audit handler creates: the fourth, after the id, the moment and the actor.
    private static List<(string File, string Argument)> OperationArgumentsOfEveryRecord() =>
    [
        .. Solution.SourceFilesOf(Audit)
            .SelectMany(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(creation => Records.Contains(creation.Type.ToString()))
                .Select(creation => (Path.GetFileName(file), OperationOf(creation)))),
    ];

    private static string OperationOf(ObjectCreationExpressionSyntax creation) =>
        creation.ArgumentList?.Arguments.SingleOrDefault(argument => argument.NameColon?.Name.Identifier.Text == "operation")?.Expression.ToString()
        ?? creation.ArgumentList?.Arguments.ElementAtOrDefault(3)?.Expression.ToString()
        ?? "<no operation>";

    // The operations in backticks in the Audit cell of each endpoint row of docs/api-catalog.md: the sixth cell, after the access.
    private static List<string> OperationsInTheCatalog() =>
    [
        .. File.ReadLines(Path.Combine(Solution.Root, "docs", "api-catalog.md"))
            .Where(line => EndpointRow().IsMatch(line))
            .SelectMany(line => CatalogOperation().Matches(line.Split('|')[6]).Select(match => match.Groups["operation"].Value))
            .Distinct()
            .Order(StringComparer.Ordinal),
    ];

    [GeneratedRegex(@"^\| \w+ \| (GET|POST|PUT|PATCH|DELETE) \|")]
    private static partial Regex EndpointRow();

    [GeneratedRegex(@"`(?<operation>[a-z_]+\.[a-z_]+)`")]
    private static partial Regex CatalogOperation();
}
