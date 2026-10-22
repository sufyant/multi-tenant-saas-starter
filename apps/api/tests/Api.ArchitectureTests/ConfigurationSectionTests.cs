using System.Reflection;

namespace Api.ArchitectureTests;

// Section 7, configuration rules 1, 6 and 9: each module has its own configuration section, and a key goes under the section of its
// owner's feature, <Module>:<Feature>:<Key>. A module names each section it binds in a Section constant, so the constants of its
// projects are every section it reads.
public sealed class ConfigurationSectionTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static TheoryData<string> Modules => [.. Solution.Modules];

    [Theory]
    [MemberData(nameof(Modules))]
    public void BindConfiguration_InAModule_IsAFeatureSectionOfTheModule(string module)
    {
        var sections = Solution.ProjectsOf(module)
            .SelectMany(project => Solution.Load(project).GetTypes())
            .SelectMany(type => type.GetFields(Declared))
            .Where(field => field is { IsLiteral: true, Name: "Section" })
            .Select(field => (string)field.GetRawConstantValue()!);

        sections.Where(section => !section.StartsWith($"{module}:", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    // Rule 6: each options class has a section of its own, so a vendor's name stays in a section of that vendor's settings only.
    [Fact]
    public void BindConfiguration_EachSection_IsNamedByOneClass()
    {
        var shared = Solution.AllProjects
            .SelectMany(project => Solution.Load(project).GetTypes())
            .SelectMany(type => type.GetFields(Declared).Where(field => field is { IsLiteral: true, Name: "Section" }).Select(field => (type, section: (string)field.GetRawConstantValue()!)))
            .GroupBy(constant => constant.section, constant => constant.type.Name)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Order())}");

        shared.ShouldBeEmpty();
    }
}
