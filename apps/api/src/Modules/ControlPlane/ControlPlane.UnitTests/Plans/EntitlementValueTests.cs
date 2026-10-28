using ControlPlane.Domain;
using ControlPlane.Domain.Plans;

namespace ControlPlane.UnitTests.Plans;

public sealed class EntitlementValueTests
{
    [Fact]
    public void Close_AFeature_IsOff() => EntitlementValue.ClosedOf(EntitlementKind.Feature).ShouldBe(EntitlementValue.Off);

    [Fact]
    public void Close_ALimit_IsZero() => EntitlementValue.ClosedOf(EntitlementKind.Limit).ShouldBe(EntitlementValue.Limit(0));

    [Theory]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(9, false)]
    public void Allow_ALimitOfFive_AllowsOneMoreOnlyUnderIt(int count, bool allowed) => EntitlementValue.Limit(5).Allows(count).ShouldBe(allowed);

    [Fact]
    public void Allow_Unlimited_AllowsAnyCount() => EntitlementValue.Unlimited.Allows(int.MaxValue).ShouldBeTrue();

    [Theory]
    [InlineData(3, 0)]
    [InlineData(5, 0)]
    [InlineData(14, 9)]
    public void MeasureExcess_ALimitOfFive_IsHowFarTheCountIsOverIt(int count, int excess) => EntitlementValue.Limit(5).ExcessOf(count).ShouldBe(excess);

    [Fact]
    public void MeasureExcess_Unlimited_IsZero() => EntitlementValue.Unlimited.ExcessOf(1000).ShouldBe(0);

    // Unlimited is a value of its own: its columns hold no number.
    [Fact]
    public void StoreUnlimited_TheColumns_HoldNoNumber()
    {
        var unlimited = EntitlementValue.Unlimited;

        (unlimited.Enabled, unlimited.Maximum, unlimited.IsUnlimited).ShouldBe((null, null, true));
    }

    [Fact]
    public void ReadColumns_AnOnFeature_IsOn() => EntitlementValue.Read(true, null, false).ShouldBe(EntitlementValue.On);

    [Fact]
    public void ReadColumns_ANumber_IsALimit() => EntitlementValue.Read(null, 20, false).ShouldBe(EntitlementValue.Limit(20));

    [Fact]
    public void ReadColumns_TwoKindsAtOnce_Throws() => Should.Throw<InvalidOperationException>(() => EntitlementValue.Read(true, 3, false));

    [Fact]
    public void CreateLimit_UnderZero_Throws() => Should.Throw<ArgumentOutOfRangeException>(() => EntitlementValue.Limit(-1));

    [Fact]
    public void CheckValue_ALimitOfAFeatureKey_IsInvalidWithTheKey()
    {
        var checkedValue = EntitlementValue.For("roles.custom", EntitlementValue.Limit(3));

        checkedValue.Error.Code.ShouldBe(ControlPlaneErrorCodes.PlanEntitlementInvalid);
        checkedValue.Error.Entitlement.ShouldBe("roles.custom");
    }

    [Fact]
    public void CheckValue_AFeatureOfALimitKey_IsInvalid() =>
        EntitlementValue.For("members.max", EntitlementValue.On).Error.Code.ShouldBe(ControlPlaneErrorCodes.PlanEntitlementInvalid);

    [Fact]
    public void CheckValue_AKeyNotOnTheList_IsInvalid() =>
        EntitlementValue.For("projects.max", EntitlementValue.Limit(1)).Error.Code.ShouldBe(ControlPlaneErrorCodes.PlanEntitlementInvalid);

    [Fact]
    public void CheckValue_UnlimitedOfALimitKey_IsTheValue() =>
        EntitlementValue.For("members.max", EntitlementValue.Unlimited).Value.ShouldBe(EntitlementValue.Unlimited);
}
