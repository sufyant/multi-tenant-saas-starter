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

    // Decision 1 of step r4b: every built-in role that invites or changes a member's role sees the roles it can give.
    [Fact]
    public void ReadRoles_EveryBuiltInRoleThatGivesARole_HoldsThePermission()
    {
        var givers = Enum.GetValues<BuiltInRole>().Where(role =>
            BuiltInRoles.PermissionsOf(role).Contains("tenant.invitations.create") || BuiltInRoles.PermissionsOf(role).Contains("tenant.members.update"));
        var readers = Enum.GetValues<BuiltInRole>().Where(role => BuiltInRoles.PermissionsOf(role).Contains("tenant.roles.read"));

        givers.ShouldBe([BuiltInRole.Owner, BuiltInRole.Admin]);
        readers.ShouldBe(givers);
    }

    // Decision 1 of step r4b: of the built-in roles, only the Owner creates, changes and deletes a custom role (GitHub and GitLab.com custom
    // roles). The permissions are not owner-only: an Owner may give them to a custom role.
    [Theory]
    [InlineData("tenant.roles.create")]
    [InlineData("tenant.roles.update")]
    [InlineData("tenant.roles.delete")]
    public void ManageRoles_OnlyTheOwner_HoldsThePermissionAndItIsNotOwnerOnly(string permission)
    {
        var holders = Enum.GetValues<BuiltInRole>().Where(role => BuiltInRoles.PermissionsOf(role).Contains(permission));

        holders.ShouldBe([BuiltInRole.Owner]);
        PermissionPools.OwnerOnly.ShouldNotContain(permission);
        PermissionPools.TenantPool.ShouldContain(permission);
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
