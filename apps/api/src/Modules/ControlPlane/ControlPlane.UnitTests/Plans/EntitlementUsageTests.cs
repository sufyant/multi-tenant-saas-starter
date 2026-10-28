using ControlPlane.Domain.Plans;

namespace ControlPlane.UnitTests.Plans;

// What each key counts lives once, here in the domain (D1): the previews and the tenant's plan read it, and the web adds nothing up.
public sealed class EntitlementUsageTests
{
    private static readonly EntitlementUsage Usage = new(CustomRoles: 3, Members: 4, PendingInvitations: 2);

    [Fact]
    public void Count_RolesCustomMax_IsTheCustomRoles() => Usage.CountOf(LimitKeys.RolesCustomMax).ShouldBe(3);

    // A feature in use: roles.custom is in use while the tenant has custom roles.
    [Fact]
    public void Count_RolesCustom_IsTheCustomRoles() => Usage.CountOf(FeatureKeys.RolesCustom).ShouldBe(3);

    [Fact]
    public void Count_MembersMax_IsTheMembersAndThePendingInvitations() => Usage.CountOf(LimitKeys.MembersMax).ShouldBe(6);

    // Every key of the list has its count, so a new key cannot be forgotten here.
    [Fact]
    public void Count_EveryKeyOfTheList_HasACount() =>
        EntitlementKeys.All.Keys.Select(key => Usage.CountOf(key)).ShouldAllBe(count => count >= 0);
}
