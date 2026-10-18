using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Audit.Api.Tenant.AuditLogs;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Solution = Api.ArchitectureTests.Fixtures.Solution;

namespace Api.ArchitectureTests;

// K3: AuditOperations declares the operation of every audit entry once. Each Audit handler names its operation from it, and the
// operation list, the Audit column of api-catalog and the specification, holds exactly its operations. OpenApiContractTests checks the
// enum in the document.
public sealed partial class AuditOperationTests
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
    public void DocumentAuditOperations_OperationList_ListsEveryOperationOfAuditOperations()
    {
        var documented = OperationsInTheList();

        documented.ShouldBe(DeclaredOperations());
    }

    // K3: every operation has a decision about its details: the one type of its details, or none. A new operation without a decision
    // fails here.
    [Fact]
    public void DeclareAuditDetails_EveryOperation_HasADecision()
    {
        var decided = AuditDetailsTypes.OfOperation.Keys.Order(StringComparer.Ordinal);

        decided.ShouldBe(DeclaredOperations());
    }

    // K3: every type of details belongs to exactly one operation.
    [Fact]
    public void DeclareAuditDetails_EveryDetailsType_BelongsToExactlyOneOperation()
    {
        var types = typeof(AuditDetails).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(AuditDetails)))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal);

        var mapped = AuditDetailsTypes.OfOperation.Values.OfType<Type>().Select(type => type.Name).Order(StringComparer.Ordinal);

        types.ShouldNotBeEmpty();
        mapped.ShouldBe(types);
    }

    // K3 and N7: every type of details has one response shape, named after it, whose discriminator is its operation.
    [Fact]
    public void AnswerAuditDetails_EveryDetailsType_HasOneResponseShapeWithItsOperation()
    {
        var expected = AuditDetailsTypes.OfOperation
            .Where(decision => decision.Value is not null)
            .Select(decision => $"{decision.Key} {decision.Value!.Name}Response");

        var answered = typeof(AuditDetailsResponse).GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(shape => $"{shape.TypeDiscriminator} {shape.DerivedType.Name}");

        answered.ShouldBe(expected, ignoreOrder: true);
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

    // The operation list: the operations in backticks in the Audit cell of each endpoint row of docs/api-catalog.md, the sixth cell, after
    // the access.
    private static List<string> OperationsInTheList() =>
    [
        .. File.ReadLines(Path.Combine(Solution.Root, "docs", "api-catalog.md"))
            .Where(line => EndpointRow().IsMatch(line))
            .SelectMany(line => ListedOperation().Matches(line.Split('|')[6]).Select(match => match.Groups["operation"].Value))
            .Distinct()
            .Order(StringComparer.Ordinal),
    ];

    [GeneratedRegex(@"^\| \w+ \| (GET|POST|PUT|PATCH|DELETE) \|")]
    private static partial Regex EndpointRow();

    [GeneratedRegex(@"`(?<operation>[a-z_]+\.[a-z_]+)`")]
    private static partial Regex ListedOperation();
}
