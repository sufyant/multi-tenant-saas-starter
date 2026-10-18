using ControlPlane.Domain;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.UnitTests.Tenants;

// K1: the Owner rules of a role change and a removal are the tenant's (M1). Each check is pure up to the change, so a rejection changes
// nothing.
public class MemberManagementTests
{
    private static readonly Guid TenantId = new("0199a8f0-0000-7000-8000-000000000001");

    [Theory]
    [InlineData("Owner", "Owner", "Admin")]
    [InlineData("Owner", "Admin", "Owner")]
    [InlineData("Owner", "Member", "Owner")]
    [InlineData("Owner", "Admin", "Member")]
    [InlineData("Admin", "Admin", "Member")]
    [InlineData("Admin", "Member", "Admin")]
    public void ChangeMemberRole_ARoleTheActorMayGive_ChangesIt(string actor, string target, string role)
    {
        var changed = Membership(target);

        var result = Tenant.ChangeMemberRole(Membership(actor), changed, RoleOf(role), owners: 2);

        result.Value.ShouldBe(StateChange.Changed);
        changed.RoleId.ShouldBe(RoleOf(role).Id);
    }

    [Theory]
    [InlineData("Owner", "Admin")]
    [InlineData("Owner", "Member")]
    [InlineData("Admin", "Owner")]
    [InlineData("Member", "Owner")]
    public void ChangeMemberRole_AnAdminOnTheOwnerRole_IsForbiddenAndChangesNothing(string target, string role)
    {
        var changed = Membership(target);

        var result = Tenant.ChangeMemberRole(Membership("Admin"), changed, RoleOf(role), owners: 2);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberOwnerRequiresOwner);
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        changed.RoleId.ShouldBe(RoleOf(target).Id);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Member")]
    public void ChangeMemberRole_TheLastOwner_IsAConflictAndKeepsTheRole(string role)
    {
        var changed = Membership("Owner");

        var result = Tenant.ChangeMemberRole(Membership("Owner"), changed, RoleOf(role), owners: 1);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberLastOwner);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        changed.RoleId.ShouldBe(BuiltInRoles.Owner.Id);
    }

    // K1: an Owner steps down when another Owner exists, and cannot as the last one.
    [Fact]
    public void ChangeMemberRole_AnOwnerStepsDownWithAnotherOwner_ChangesIt()
    {
        var self = Membership("Owner");

        var result = Tenant.ChangeMemberRole(self, self, BuiltInRoles.Admin, owners: 2);

        result.Value.ShouldBe(StateChange.Changed);
        self.RoleId.ShouldBe(BuiltInRoles.Admin.Id);
    }

    [Fact]
    public void ChangeMemberRole_TheLastOwnerStepsDown_IsAConflict()
    {
        var self = Membership("Owner");

        var result = Tenant.ChangeMemberRole(self, self, BuiltInRoles.Admin, owners: 1);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberLastOwner);
        self.RoleId.ShouldBe(BuiltInRoles.Owner.Id);
    }

    // K1: an Admin may become a Member, and cannot make themselves an Owner.
    [Fact]
    public void ChangeMemberRole_AnAdminBecomesAMember_ChangesIt()
    {
        var self = Membership("Admin");

        var result = Tenant.ChangeMemberRole(self, self, BuiltInRoles.Member, owners: 1);

        result.Value.ShouldBe(StateChange.Changed);
    }

    [Fact]
    public void ChangeMemberRole_AnAdminMakesThemselvesAnOwner_IsForbidden()
    {
        var self = Membership("Admin");

        var result = Tenant.ChangeMemberRole(self, self, BuiltInRoles.Owner, owners: 1);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberOwnerRequiresOwner);
        self.RoleId.ShouldBe(BuiltInRoles.Admin.Id);
    }

    // K1 and I1: the role the member already has changes nothing. The last Owner keeps the Owner role, which is no loss.
    [Theory]
    [InlineData("Owner", 1)]
    [InlineData("Admin", 1)]
    [InlineData("Member", 1)]
    public void ChangeMemberRole_TheRoleTheMemberHas_ChangesNothing(string role, int owners)
    {
        var result = Tenant.ChangeMemberRole(Membership("Owner"), Membership(role), RoleOf(role), owners);

        result.Value.ShouldBe(StateChange.Unchanged);
    }

    // Section 6, "Member management": an Admin who acts on an Owner is refused before the Owner count is looked at.
    [Fact]
    public void ChangeMemberRole_AnAdminOnTheLastOwner_IsForbiddenFirst()
    {
        var result = Tenant.ChangeMemberRole(Membership("Admin"), Membership("Owner"), BuiltInRoles.Member, owners: 1);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberOwnerRequiresOwner);
    }

    [Theory]
    [InlineData("Owner", "Owner")]
    [InlineData("Owner", "Admin")]
    [InlineData("Owner", "Member")]
    [InlineData("Admin", "Admin")]
    [InlineData("Admin", "Member")]
    public void RemoveMember_AMemberTheActorMayRemove_IsAllowed(string actor, string target)
    {
        var result = Tenant.CheckRemoval(Membership(actor), Membership(target), owners: 2);

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    public void RemoveMember_Themselves_IsForbidden(string role)
    {
        var self = Membership(role);

        var result = Tenant.CheckRemoval(self, self, owners: 2);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberCannotRemoveSelf);
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public void RemoveMember_AnAdminRemovesAnOwner_IsForbidden()
    {
        var result = Tenant.CheckRemoval(Membership("Admin"), Membership("Owner"), owners: 2);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberOwnerRequiresOwner);
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    // Only the count of Owners decides: a tenant whose Owner count is one keeps that Owner, whoever acts.
    [Fact]
    public void RemoveMember_TheLastOwner_IsAConflict()
    {
        var result = Tenant.CheckRemoval(Membership("Owner"), Membership("Owner"), owners: 1);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberLastOwner);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    // Section 6, "Member management": removing yourself is refused before any other check, also for the last Owner.
    [Fact]
    public void RemoveMember_TheLastOwnerThemselves_IsForbiddenFirst()
    {
        var self = Membership("Owner");

        var result = Tenant.CheckRemoval(self, self, owners: 1);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberCannotRemoveSelf);
    }

    private static Role RoleOf(string role) => BuiltInRoles.All.Single(builtIn => builtIn.BuiltIn.ToString() == role);

    private static Membership Membership(string role) => new(TenantId, Guid.NewGuid(), RoleOf(role).Id, DateTimeOffset.UnixEpoch);
}
