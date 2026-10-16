using FluentValidation;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharedKernel;

namespace Api.ArchitectureTests;

// K5: a screen writes a text for each code of the one enum, so no answer may carry a code the enum does not hold. An Error takes an
// ErrorCode, and only a static readonly field of an ...ErrorCodes class declares one, so every code of a result is a declared code,
// and the document lists every declared code (OpenApiContractTests). What is left is the code member of Problem Details itself:
// only the host's Problems file writes it, and it writes only ErrorCode values. OpenApiContract names it too, to describe it in the
// document. Read from the source tree, as RouteGroupPlaceTests does.
public sealed class ErrorCodeTests
{
    private static readonly string ProblemsFile = Path.Combine("src", "Api", "ErrorHandling", "Problems.cs");

    private static readonly string OpenApiContractFile = Path.Combine("src", "Api", "Hosting", "OpenApiContract.cs");

    [Fact]
    public void WriteErrorCode_EveryProductionSourceFile_NamesTheCodeMemberOnlyInProblemsAndTheDocument()
    {
        var sourceFolder = Path.Combine(Solution.Root, "src") + Path.DirectorySeparatorChar;

        var writers = Solution.ProductionProjects
            .Where(project => Solution.FolderOf(project).StartsWith(sourceFolder, StringComparison.Ordinal))
            .SelectMany(Solution.SourceFilesOf)
            .Where(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantTokens()
                .Any(token => (token.RawKind == (int)SyntaxKind.StringLiteralToken && token.ValueText == "code") || token.ValueText == "CodeMember"))
            .Select(file => Path.GetRelativePath(Solution.Root, file));

        writers.ShouldBe([ProblemsFile, OpenApiContractFile], ignoreOrder: true);
    }

    // Each owner declares its codes in one static class whose name ends with ErrorCodes (SharedKernel, the host, each module's Domain).
    [Fact]
    public void DeclareErrorCode_EveryProductionSourceFile_DeclaresOnlyStaticReadonlyFieldsOfAnErrorCodesClass()
    {
        var sourceFolder = Path.Combine(Solution.Root, "src") + Path.DirectorySeparatorChar;

        var elsewhere = Solution.ProductionProjects
            .Where(project => Solution.FolderOf(project).StartsWith(sourceFolder, StringComparison.Ordinal))
            .SelectMany(Solution.SourceFilesOf)
            .SelectMany(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(call => call.Expression.ToString() == "ErrorCode.Declare")
                .Where(call => !IsAnErrorCodesField(call))
                .Select(call => $"{Path.GetRelativePath(Solution.Root, file)}: {call}"));

        ErrorCodeDeclarations.All.ShouldNotBeEmpty();
        elsewhere.ShouldBeEmpty();
    }

    // One code has one owner, so the screen's text for it means one thing.
    [Fact]
    public void DeclareErrorCode_EveryCode_HasOneDeclaration()
    {
        var twice = ErrorCodeDeclarations.All
            .GroupBy(declared => declared.Value)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(declared => declared.Owner))}");

        twice.ShouldBeEmpty();
    }

    // A validation answer lists a field code for each failed rule, so every rule of every validator states one.
    [Fact]
    public void ValidateCommand_EveryRuleOfEveryValidator_StatesAFieldErrorCode()
    {
        var validators = Solution.ModuleProjects
            .Where(Solution.IsShipped)
            .Select(Solution.Load)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsClass: true } && typeof(IValidator).IsAssignableFrom(type))
            .ToList();

        var withoutCode = validators
            .SelectMany(type => ((IValidator)Activator.CreateInstance(type)!).CreateDescriptor().Rules
                .SelectMany(rule => rule.Components.Select(component => (Rule: $"{type.Name} {rule.PropertyName} {component.Validator.Name}", component.ErrorCode))))
            .Where(rule => !Enum.GetNames<FieldErrorCode>().Contains(rule.ErrorCode))
            .Select(rule => $"{rule.Rule}: '{rule.ErrorCode}'");

        validators.ShouldNotBeEmpty();
        withoutCode.ShouldBeEmpty();
    }

    private static bool IsAnErrorCodesField(InvocationExpressionSyntax call) =>
        call.Ancestors().OfType<FieldDeclarationSyntax>().FirstOrDefault() is { } field
        && field.Modifiers.Any(modifier => modifier.Text == "static")
        && field.Modifiers.Any(modifier => modifier.Text == "readonly")
        && field.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.Text.EndsWith("ErrorCodes", StringComparison.Ordinal) == true;
}
