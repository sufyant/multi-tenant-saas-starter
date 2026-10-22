using ControlPlane.Domain;
using ControlPlane.Domain.Roles;

namespace ControlPlane.UnitTests.Roles;

public sealed class BuiltInRolesTests
{
    // Requests, responses, events and audit details name a built-in role in lower case, as every other enum of the API (api-catalog, K5).
    [Fact]
    public void NameBuiltInRoles_EveryRole_IsItsLowerCaseName()
    {
        var names = Enum.GetValues<BuiltInRole>().Select(Role.NameOf);

        names.ShouldBe(["owner", "admin", "member"]);
    }

    [Fact]
    public void GrantBuiltInRoles_EveryRole_HoldsNoSystemPermission()
    {
        var platformPermissions = Enum.GetValues<BuiltInRole>().SelectMany(BuiltInRoles.PermissionsOf).Intersect(PermissionPools.PlatformPool);

        platformPermissions.ShouldBeEmpty();
    }

    [Fact]
    public void ReadMembers_EveryBuiltInRole_HoldsThePermission()
    {
        var roles = Enum.GetValues<BuiltInRole>();

        roles.ShouldAllBe(role => BuiltInRoles.PermissionsOf(role).Contains("tenant.members.read"));
    }

    [Fact]
    public void ReadTenantProfile_EveryBuiltInRole_HoldsThePermission()
    {
        var roles = Enum.GetValues<BuiltInRole>();

        roles.ShouldAllBe(role => BuiltInRoles.PermissionsOf(role).Contains("tenant.profile.read"));
    }

    // The invitations are the Owners' and the Admins' work (K1); a Member holds none of their permissions.
    [Theory]
    [InlineData("tenant.invitations.create")]
    [InlineData("tenant.invitations.read")]
    [InlineData("tenant.invitations.delete")]
    public void ManageInvitations_OwnerAndAdmin_HoldThePermissionAndAMemberDoesNot(string permission)
    {
        var holders = Enum.GetValues<BuiltInRole>().Where(role => BuiltInRoles.PermissionsOf(role).Contains(permission));

        holders.ShouldBe([BuiltInRole.Owner, BuiltInRole.Admin]);
    }

    // Changing a role and removing a member are the Owners' and the Admins' work (K1, K7).
    [Theory]
    [InlineData("tenant.members.update")]
    [InlineData("tenant.members.delete")]
    public void ManageMembers_OwnerAndAdmin_HoldThePermissionAndAMemberDoesNot(string permission)
    {
        var holders = Enum.GetValues<BuiltInRole>().Where(role => BuiltInRoles.PermissionsOf(role).Contains(permission));

        holders.ShouldBe([BuiltInRole.Owner, BuiltInRole.Admin]);
    }

    // Activity, the tenant's audit log, is the Owners' and the Admins' to read (api-catalog, step 20f; K7).
    [Fact]
    public void ReadAuditLog_OwnerAndAdmin_HoldThePermissionAndAMemberDoesNot()
    {
        var holders = Enum.GetValues<BuiltInRole>().Where(role => BuiltInRoles.PermissionsOf(role).Contains("tenant.audit.read"));

        holders.ShouldBe([BuiltInRole.Owner, BuiltInRole.Admin]);
    }

    // Giving the Owner role, changing or removing an Owner and inviting an Owner are the Owners' alone (K1).
    [Fact]
    public void ManageOwners_OnlyTheOwner_HoldsThePermission()
    {
        var holders = Enum.GetValues<BuiltInRole>().Where(role => BuiltInRoles.PermissionsOf(role).Contains("tenant.owners.manage"));

        holders.ShouldBe([BuiltInRole.Owner]);
    }

    // A custom role chooses from the tenant pool, but never a permission that only the Owner role holds (A3).
    [Fact]
    public void ListOwnerOnlyPermissions_TheTenantPool_HoldsThemAndOnlyTheOwnerHoldsThem()
    {
        var ownerOnly = PermissionPools.OwnerOnly;

        ownerOnly.ShouldBe(["tenant.owners.manage"]);
        ownerOnly.ShouldBeSubsetOf(PermissionPools.TenantPool);
        Enum.GetValues<BuiltInRole>().Where(role => BuiltInRoles.PermissionsOf(role).Overlaps(ownerOnly)).ShouldBe([BuiltInRole.Owner]);
    }
}
