using System.Reflection;
using System.Text.Json;
using ControlPlane.Api.Tenant;

namespace Api.ArchitectureTests;

// The maintainer's decision in audit fix 04: a built-in role is one word in every place that names it. The API's MemberRole and the
// domain's BuiltInRole and Role.NameOf each write the words; Audit entries the word of ControlPlane's events. The API and the domain are
// coupled by the member's name (ToString and Enum.Parse), so a renamed member compiles and throws at run time. The domain is internal
// to its module, and only a module's own projects see its internals (InternalsVisibleToTests), so this system-wide test reads it by
// reflection.
public class RoleNameTests
{
    private static readonly Type BuiltInRole = Solution.Load("ControlPlane.Domain").GetType("ControlPlane.Domain.Roles.BuiltInRole", throwOnError: true)!;

    private static readonly MethodInfo DomainNameOf =
        Solution.Load("ControlPlane.Domain").GetType("ControlPlane.Domain.Roles.Role", throwOnError: true)!.GetMethod("NameOf", BindingFlags.Public | BindingFlags.Static)!;

    public static TheoryData<MemberRole> MemberRoles => [.. Enum.GetValues<MemberRole>()];

    [Fact]
    public void NameABuiltInRole_TheDomainAndTheApi_HaveTheSameMembers()
    {
        Enum.GetNames(BuiltInRole).ShouldBe(Enum.GetNames<MemberRole>(), ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(MemberRoles))]
    public void NameABuiltInRole_EveryMemberRole_IsOneWordInTheApiAndTheDomain(MemberRole role)
    {
        var json = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(role));
        var builtIn = Enum.Parse(BuiltInRole, role.ToString());

        DomainNameOf.Invoke(null, [builtIn]).ShouldBe(json);
    }
}
