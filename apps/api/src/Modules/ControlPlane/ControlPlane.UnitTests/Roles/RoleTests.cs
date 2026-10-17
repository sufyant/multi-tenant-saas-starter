using ControlPlane.Domain;
using ControlPlane.Domain.Roles;

namespace ControlPlane.UnitTests.Roles;

public class RoleTests
{
    // Requests, responses, events and audit details name a built-in role in lower case, as every other enum of the API (catalog, K5).
    [Fact]
    public void NameBuiltInRoles_EveryRole_IsItsLowerCaseName()
    {
        var names = BuiltInRoles.All.Select(role => role.Name);

        names.ShouldBe(["owner", "admin", "member"]);
    }

    [Fact]
    public void GrantBuiltInRoles_EveryRole_HoldsNoSystemPermission()
    {
        var systemPermissions = BuiltInRoles.All.SelectMany(role => role.Permissions).Intersect(PermissionPools.SystemPool);

        systemPermissions.ShouldBeEmpty();
    }

    [Fact]
    public void ReadMembers_EveryBuiltInRole_HoldsThePermission()
    {
        var roles = BuiltInRoles.All;

        roles.ShouldAllBe(role => role.Permissions.Contains("tenant.members.read"));
    }

    [Fact]
    public void ReadTenantProfile_EveryBuiltInRole_HoldsThePermission()
    {
        var roles = BuiltInRoles.All;

        roles.ShouldAllBe(role => role.Permissions.Contains("tenant.profile.read"));
    }

    // The invitations are the Owners' and the Admins' work (K1); a Member holds none of their permissions.
    [Theory]
    [InlineData("tenant.invitations.create")]
    [InlineData("tenant.invitations.read")]
    [InlineData("tenant.invitations.delete")]
    public void ManageInvitations_OwnerAndAdmin_HoldThePermissionAndAMemberDoesNot(string permission)
    {
        var holders = BuiltInRoles.All.Where(role => role.Permissions.Contains(permission)).Select(role => role.BuiltIn);

        holders.ShouldBe([BuiltInRole.Owner, BuiltInRole.Admin]);
    }

    // Changing a role and removing a member are the Owners' and the Admins' work (K1, K7).
    [Theory]
    [InlineData("tenant.members.update")]
    [InlineData("tenant.members.delete")]
    public void ManageMembers_OwnerAndAdmin_HoldThePermissionAndAMemberDoesNot(string permission)
    {
        var holders = BuiltInRoles.All.Where(role => role.Permissions.Contains(permission)).Select(role => role.BuiltIn);

        holders.ShouldBe([BuiltInRole.Owner, BuiltInRole.Admin]);
    }

    // Activity, the tenant's audit log, is the Owners' and the Admins' to read (catalog, step 20f; K7).
    [Fact]
    public void ReadAuditLog_OwnerAndAdmin_HoldThePermissionAndAMemberDoesNot()
    {
        var holders = BuiltInRoles.All.Where(role => role.Permissions.Contains("tenant.audit.read")).Select(role => role.BuiltIn);

        holders.ShouldBe([BuiltInRole.Owner, BuiltInRole.Admin]);
    }
}
