using System.Reflection;
using Api.IntegrationTests.Fixtures;
using Microsoft.CodeAnalysis;
using Solution = Api.ArchitectureTests.Fixtures.Solution;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Api.ArchitectureTests;

// Section 9, rule 10: a test class whose fake clock starts at a fixed date says why with [FixedClock]. On the shared database a closure
// schedules its messages by the real clock at the fake date plus weeks, so a fixed date in the past makes them due in a later run, and a
// host of another test runs them. A clock starts where FakeTimeProvider is created or set. It starts at the real now when its date is read
// from the real clock, directly or through a field or a local of the same class; any other date is fixed, and so is FakeTimeProvider's
// own start, 2000-01-01.
public sealed class FixedClockTests
{
    private const string IntegrationTests = "Api.IntegrationTests";

    private static readonly string[] RealNow = ["UtcNow", "GetUtcNow"];

    [Fact]
    public void StartFakeClock_TestClassOnAFixedDate_StatesWhy()
    {
        var withoutAReason = Solution.SourceFilesOf(IntegrationTests)
            .SelectMany(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Where(type => !type.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => attribute.Name.ToString() == "FixedClock"))
            .Where(type => ClockStarts(type).Any(start => !StartsAtTheRealNow(type, start)))
            .Select(type => type.Identifier.Text);

        withoutAReason.ShouldBeEmpty();
    }

    [Fact]
    public void MarkFixedClock_TestClass_StatesItsReason()
    {
        var withoutAReason = typeof(FixedClockAttribute).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<FixedClockAttribute>() is { } marker && string.IsNullOrWhiteSpace(marker.Reason))
            .Select(type => type.Name);

        withoutAReason.ShouldBeEmpty();
    }

    // The argument lists that start a fake clock: its creation, as new FakeTimeProvider(...) or new(...) of a FakeTimeProvider, and SetUtcNow.
    private static IEnumerable<ArgumentListSyntax> ClockStarts(ClassDeclarationSyntax type) =>
        type.DescendantNodes().Select(node => node switch
        {
            ObjectCreationExpressionSyntax creation when creation.Type.ToString() == "FakeTimeProvider" =>
                creation.ArgumentList ?? SyntaxFactory.ArgumentList(),
            ImplicitObjectCreationExpressionSyntax creation when DeclaredType(creation) == "FakeTimeProvider" => creation.ArgumentList,
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "SetUtcNow" } } invocation =>
                invocation.ArgumentList,
            _ => null,
        }).OfType<ArgumentListSyntax>();

    private static string? DeclaredType(ImplicitObjectCreationExpressionSyntax creation) =>
        creation.Parent?.Parent?.Parent is VariableDeclarationSyntax declaration ? declaration.Type.ToString() : null;

    // The date reads the real clock, directly or through the initializers of the fields and locals it names.
    private static bool StartsAtTheRealNow(ClassDeclarationSyntax type, ArgumentListSyntax start)
    {
        var initializers = type.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(variable => variable.Initializer is not null)
            .ToLookup(variable => variable.Identifier.Text, variable => variable.Initializer!.Value);
        var read = new HashSet<SyntaxNode>();
        var pending = new Queue<SyntaxNode>(start.Arguments.Select(argument => argument.Expression));
        while (pending.TryDequeue(out var expression))
        {
            if (!read.Add(expression))
            {
                continue;
            }

            foreach (var name in expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            {
                if (RealNow.Contains(name.Identifier.Text))
                {
                    return true;
                }

                foreach (var initializer in initializers[name.Identifier.Text])
                {
                    pending.Enqueue(initializer);
                }
            }
        }

        return false;
    }
}
