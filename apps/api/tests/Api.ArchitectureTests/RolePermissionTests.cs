using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Api.ArchitectureTests;

// A1: the code asks for a permission, never for a role. A rule of the Owners asks for tenant.owners.manage (K1), and every Owner count
// counts the members whose role holds it. A built-in role is named in code only where a role is assigned or shown, so a rule that
// compares with a role fails here. Roles/, which declares the built-in roles and their permissions, is the one place that names them
// all. Read from the source tree, as ErrorCodeTests does.
public sealed class RolePermissionTests
{
    private static readonly string[] BuiltInRoleTypes = ["BuiltInRole", "BuiltInRoles"];

    private static readonly string[] BuiltInRoleNames = ["Owner", "Admin", "Member"];

    // Each place that may name a built-in role, and why. Nothing here decides what a member may do.
    private static readonly string[] NamingPlaces =
    [
        // Assignments: a new tenant's first Owner is invited with the Owner role, and so is the Owner the platform invites (K9).
        Path.Combine("src", "Modules", "ControlPlane", "ControlPlane.Application", "Tenants", "CreateTenant", "CreateTenantHandler.cs"),
        Path.Combine("src", "Modules", "ControlPlane", "ControlPlane.Application", "Invitations", "InviteOwner", "InviteOwnerHandler.cs"),

        // An assignment: the platform makes a member an Owner (docs/flows/owner-recovery.md).
        Path.Combine("src", "Modules", "ControlPlane", "ControlPlane.Application", "Members", "MakeOwner", "MakeOwnerHandler.cs"),

        // The member list's order by role shows the Owners, then the Admins, then the Members.
        Path.Combine("src", "Modules", "ControlPlane", "ControlPlane.Infrastructure", "Persistence", "Members", "MemberQueries.cs"),
    ];

    [Fact]
    public void NameABuiltInRole_EveryControlPlaneSourceFile_NamesItOnlyWhereARoleIsAssignedOrShown()
    {
        var roles = Path.Combine(Solution.Root, "src", "Modules", "ControlPlane", "ControlPlane.Domain", "Roles") + Path.DirectorySeparatorChar;

        var naming = Solution.ProductionProjects
            .Where(project => project.StartsWith("ControlPlane.", StringComparison.Ordinal))
            .SelectMany(Solution.SourceFilesOf)
            .Where(file => !file.StartsWith(roles, StringComparison.Ordinal))
            .Where(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Any(access => BuiltInRoleTypes.Contains(access.Expression.ToString()) && BuiltInRoleNames.Contains(access.Name.Identifier.ValueText)))
            .Select(file => Path.GetRelativePath(Solution.Root, file));

        naming.ShouldBe(NamingPlaces, ignoreOrder: true);
    }
}
