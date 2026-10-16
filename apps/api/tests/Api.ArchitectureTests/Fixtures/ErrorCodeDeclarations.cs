using System.Reflection;
using SharedKernel;

namespace Api.ArchitectureTests.Fixtures;

// K5: every error code the shipped projects declare, read from their static classes whose names end with ErrorCodes.
internal static class ErrorCodeDeclarations
{
    public static IReadOnlyList<(string Owner, string Value)> All { get; } =
    [
        .. Solution.ProductionProjects
            .Where(Solution.IsShipped)
            .Select(Solution.Load)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: true, IsSealed: true } && type.Name.EndsWith("ErrorCodes", StringComparison.Ordinal))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(ErrorCode))
                .Select(field => ($"{type.FullName}.{field.Name}", ((ErrorCode)field.GetValue(null)!).Value))),
    ];
}
