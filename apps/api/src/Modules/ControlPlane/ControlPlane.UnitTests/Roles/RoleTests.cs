using System.Collections.Frozen;
using ControlPlane.Domain;
using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.UnitTests.Roles;

// A role's permissions, built in or custom (A2, A3), and the one rule against privilege escalation (decision 2 of step r4b).
public sealed class RoleTests
{
    private static readonly Guid TenantId = new("0199a8f0-0000-7000-8000-000000000001");

    private static readonly Role Owner = Role.CreateBuiltIn(Guid.NewGuid(), TenantId, BuiltInRole.Owner);

    private static readonly Role Admin = Role.CreateBuiltIn(Guid.NewGuid(), TenantId, BuiltInRole.Admin);

    private static readonly Role Member = Role.CreateBuiltIn(Guid.NewGuid(), TenantId, BuiltInRole.Member);

    [Fact]
    public void CheckEscalation_AnActorWhoHoldsExactlyThePermissions_IsAllowed()
    {
        var actor = Custom("Lead", PermissionPools.TenantMembersRead, PermissionPools.TenantMembersUpdate);

        var allowed = Role.CheckEscalation(actor, [PermissionPools.TenantMembersUpdate, PermissionPools.TenantMembersRead]);

        allowed.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckEscalation_AnActorWhoMissesOnePermission_IsRefused()
    {
        var actor = Custom("Lead", PermissionPools.TenantMembersRead, PermissionPools.TenantMembersUpdate);

        var allowed = Role.CheckEscalation(actor, [PermissionPools.TenantMembersRead, PermissionPools.TenantAuditRead]);

        allowed.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleEscalation);
        allowed.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    // An Owner holds the whole tenant pool, so the rule never limits an Owner.
    [Fact]
    public void CheckEscalation_AnOwnerOnTheWholeTenantPool_IsAllowed()
    {
        var allowed = Role.CheckEscalation(Owner, PermissionPools.TenantPool);

        allowed.IsSuccess.ShouldBeTrue();
    }

    // Today's behaviour stays: an Admin does not act on an Owner.
    [Fact]
    public void CheckEscalation_AnAdminOnTheOwnersPermissions_IsRefused()
    {
        var allowed = Role.CheckEscalation(Admin, Owner.Permissions);

        allowed.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleEscalation);
    }

    // Finding 7 of step r4b: every role reads its tenant, so a custom role holds tenant.profile.read.
    [Fact]
    public void CheckRequiredPermissions_NoProfileRead_IsInvalid()
    {
        var allowed = Role.CheckRequiredPermissions([PermissionPools.TenantMembersRead]);

        allowed.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleProfileReadRequired);
        allowed.Error.Type.ShouldBe(ErrorType.Validation);
    }

    // Finding 7 of step r4b and step r12: a role that acts on a member's or an invitation's role reads the roles, because the
    // escalation rule makes the screen read the target's role.
    [Theory]
    [InlineData(PermissionPools.TenantInvitationsCreate)]
    [InlineData(PermissionPools.TenantMembersUpdate)]
    [InlineData(PermissionPools.TenantMembersDelete)]
    [InlineData(PermissionPools.TenantInvitationsDelete)]
    public void CheckRequiredPermissions_APermissionThatActsOnARoleWithoutRolesRead_IsInvalid(string actsOnARole)
    {
        var allowed = Role.CheckRequiredPermissions([PermissionPools.TenantProfileRead, actsOnARole]);

        allowed.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleRolesReadRequired);
        allowed.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Theory]
    [InlineData(PermissionPools.TenantInvitationsCreate)]
    [InlineData(PermissionPools.TenantMembersUpdate)]
    [InlineData(PermissionPools.TenantMembersDelete)]
    [InlineData(PermissionPools.TenantInvitationsDelete)]
    public void CheckRequiredPermissions_APermissionThatActsOnARoleWithRolesRead_IsAllowed(string actsOnARole)
    {
        var allowed = Role.CheckRequiredPermissions([PermissionPools.TenantProfileRead, PermissionPools.TenantRolesRead, actsOnARole]);

        allowed.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckRequiredPermissions_ProfileReadAlone_IsAllowed()
    {
        var allowed = Role.CheckRequiredPermissions([PermissionPools.TenantProfileRead]);

        allowed.IsSuccess.ShouldBeTrue();
    }

    // The built-in roles keep the same rule, so a custom role asks nothing a built-in role does not hold.
    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    [InlineData("Member")]
    public void CheckRequiredPermissions_ABuiltInRole_IsAllowed(string builtIn)
    {
        var allowed = Role.CheckRequiredPermissions(BuiltInRoles.PermissionsOf(Enum.Parse<BuiltInRole>(builtIn)));

        allowed.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void HoldPermission_ACustomRole_HoldsItsRowsAndNothingElse()
    {
        var role = Custom("Billing", PermissionPools.TenantAuditRead);

        role.Holds(PermissionPools.TenantAuditRead).ShouldBeTrue();
        role.Holds(PermissionPools.TenantMembersRead).ShouldBeFalse();
    }

    // A1: a query finds the roles that hold a permission, a custom role with a row of it included.
    [Fact]
    public void FindRolesHolding_APermissionOfACustomRole_FindsTheBuiltInHoldersAndTheCustomRole()
    {
        var billing = Custom("Billing", PermissionPools.TenantMembersUpdate);
        var reader = Custom("Reader", PermissionPools.TenantAuditRead);

        var holders = new[] { Owner, Admin, Member, billing, reader }.AsQueryable().Where(Role.Holding(PermissionPools.TenantMembersUpdate));

        holders.ShouldBe([Owner, Admin, billing]);
    }

    // A3: no custom role holds an owner-only permission, so the query finds only the Owner, and reads no row of role_permissions.
    [Fact]
    public void FindRolesHolding_TheOwnersPermission_FindsOnlyTheOwner()
    {
        var everything = Custom("Everything", [.. PermissionPools.TenantPool.Except(PermissionPools.OwnerOnly)]);

        var holders = new[] { Owner, Admin, Member, everything }.AsQueryable().Where(Role.Holding(PermissionPools.TenantOwnersManage));

        holders.ShouldBe([Owner]);
    }

    private static Role Custom(string name, params string[] permissions) =>
        Role.CreateCustom(Guid.NewGuid(), TenantId, name, permissions.ToFrozenSet(StringComparer.Ordinal));
}
