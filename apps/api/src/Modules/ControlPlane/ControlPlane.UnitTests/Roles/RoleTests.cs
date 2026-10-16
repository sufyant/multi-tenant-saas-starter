using ControlPlane.Domain;
using ControlPlane.Domain.Roles;

namespace ControlPlane.UnitTests.Roles;

public class RoleTests
{
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
}
