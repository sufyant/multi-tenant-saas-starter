using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Runtime.Handlers;
using Xunit.v3;

namespace Api.IntegrationTests.Fixtures;

// The application loads the handler code that `codegen write` committed (W10). A host that adds handlers or handler policies of a
// test has chains that code does not hold, so it generates and compiles its handler code at runtime instead. Each such host keeps
// its compiled code and the compiler's memory to the end of the run (section 9, rule 11). Its test class carries
// [RuntimeHandlerCode], so CI runs it apart from the hosts on the committed code (scripts/ci/test-api.sh).
public static class RuntimeHandlerCode
{
    public static WolverineOptions GenerateHandlerCodeAtRuntime(this WolverineOptions options)
    {
        var testClass = TestContext.Current.TestClass;
        if (testClass?.Traits.GetValueOrDefault(RuntimeHandlerCodeAttribute.Trait)?.Contains(RuntimeHandlerCodeAttribute.Value) != true)
        {
            throw new InvalidOperationException(
                $"{testClass?.TestClassName ?? "A test"} generates its handler code at runtime. Mark the class [RuntimeHandlerCode], " +
                "so CI runs it with the other tests on runtime code.");
        }

        options.CodeGeneration.TypeLoadMode = TypeLoadMode.Dynamic;
        return options.UseRuntimeCompilation();
    }

    // A test policy that adds a frame to a chain of a host on the committed code never runs: Wolverine finds the committed type by its
    // name and does not compare its code. So such a host does not start. A test handler added with Discovery.IncludeType needs no
    // check: Wolverine finds no committed type for its chain and the host does not start (MissingPreBuiltTypesException).
    public static void AssertTheCommittedCodeHoldsEveryFrame(IServiceProvider services)
    {
        if (services.GetRequiredService<WolverineOptions>().CodeGeneration.TypeLoadMode != TypeLoadMode.Static)
        {
            return;
        }

        var graph = services.GetRequiredService<HandlerGraph>();
        var frames = graph.Chains.Concat(graph.Chains.SelectMany(chain => chain.ByEndpoint))
            .SelectMany(chain => chain.Middleware.Concat(chain.Postprocessors).Where(IsOfATest).Select(frame => $"{chain.MessageType.Name}: {frame}"))
            .ToList();
        if (frames.Count > 0)
        {
            throw new InvalidOperationException(
                $"A test policy changed the handler code, and the host loads the committed code. Call {nameof(GenerateHandlerCodeAtRuntime)} " +
                $"where the policy is added. Frames: {string.Join(", ", frames)}");
        }
    }

    private static bool IsOfATest(Frame frame) =>
        frame.GetType().Assembly == typeof(RuntimeHandlerCode).Assembly
        || (frame is MethodCall call && call.HandlerType.Assembly == typeof(RuntimeHandlerCode).Assembly);
}

// Marks a test class whose hosts generate their handler code at runtime. CI runs these classes in a part of their own, because each
// of their hosts keeps much more memory than a host on the committed code (scripts/ci/test-api.sh).
[AttributeUsage(AttributeTargets.Class)]
public sealed class RuntimeHandlerCodeAttribute : Attribute, ITraitAttribute
{
    public const string Trait = "HandlerCode";

    public const string Value = "Runtime";

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new(Trait, Value)];
}
