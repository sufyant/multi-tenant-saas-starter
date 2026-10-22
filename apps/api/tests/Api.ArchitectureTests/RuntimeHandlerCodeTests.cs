using System.Reflection;
using Api.IntegrationTests.Fixtures;

namespace Api.ArchitectureTests;

// Section 9, rule 11: a test host compiles its handler code at runtime only for a test-only handler, message or policy, and its test
// class says which. The attribute takes the reason, so a class without one does not build; this test refuses an empty one.
public sealed class RuntimeHandlerCodeTests
{
    [Fact]
    public void MarkRuntimeHandlerCode_TestClass_StatesItsReason()
    {
        var withoutAReason = typeof(RuntimeHandlerCodeAttribute).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<RuntimeHandlerCodeAttribute>() is { } marker && string.IsNullOrWhiteSpace(marker.Reason))
            .Select(type => type.Name);

        withoutAReason.ShouldBeEmpty();
    }
}
