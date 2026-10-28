using ControlPlane.Domain;
using ControlPlane.Domain.Plans;

namespace ControlPlane.UnitTests.Plans;

public sealed class EffectiveEntitlementsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, EntitlementValue> Pro = new()
    {
        ["roles.custom"] = EntitlementValue.On,
        ["roles.custom.max"] = EntitlementValue.Limit(20),
        ["members.max"] = EntitlementValue.Unlimited,
    };

    [Fact]
    public void Compute_NoOverride_IsThePlansValue()
    {
        var effective = EffectiveEntitlements.Of(Pro, [], Now);

        effective.ValueOf("roles.custom.max").ShouldBe(EntitlementValue.Limit(20));
    }

    [Fact]
    public void Compute_AnOverride_IsTheOverridesValue()
    {
        var effective = EffectiveEntitlements.Of(Pro, [new OverrideValue("roles.custom.max", EntitlementValue.Limit(50), null)], Now);

        effective.ValueOf("roles.custom.max").ShouldBe(EntitlementValue.Limit(50));
    }

    [Fact]
    public void Compute_AnOverrideBeforeItsExpiry_Applies()
    {
        var effective = EffectiveEntitlements.Of(Pro, [new OverrideValue("roles.custom", EntitlementValue.Off, Now.AddTicks(1))], Now);

        effective.ValueOf("roles.custom").ShouldBe(EntitlementValue.Off);
    }

    // An override stops at its expiry itself, so the plan's value applies from that moment.
    [Fact]
    public void Compute_AnOverrideAtItsExpiry_IsThePlansValue()
    {
        var effective = EffectiveEntitlements.Of(Pro, [new OverrideValue("roles.custom", EntitlementValue.Off, Now)], Now);

        effective.ValueOf("roles.custom").ShouldBe(EntitlementValue.On);
    }

    [Fact]
    public void Compute_AFeatureThePlanLacks_IsOff()
    {
        var effective = EffectiveEntitlements.Of(new Dictionary<string, EntitlementValue>(), [], Now);

        effective.ValueOf("roles.custom").ShouldBe(EntitlementValue.Off);
    }

    [Fact]
    public void Compute_ALimitThePlanLacks_IsZero()
    {
        var effective = EffectiveEntitlements.Of(new Dictionary<string, EntitlementValue>(), [], Now);

        effective.ValueOf("members.max").ShouldBe(EntitlementValue.Limit(0));
    }

    [Fact]
    public void Compute_Unlimited_StaysUnlimited() => EffectiveEntitlements.Of(Pro, [], Now).ValueOf("members.max").ShouldBe(EntitlementValue.Unlimited);

    [Fact]
    public void Compute_EveryKeyOfTheList_HasAValue() =>
        EffectiveEntitlements.Of(new Dictionary<string, EntitlementValue>(), [], Now).Values.Keys.ShouldBe(EntitlementKeys.All.Keys, ignoreOrder: true);

    [Fact]
    public void CheckFeature_Off_IsForbiddenWithTheKey()
    {
        var feature = EffectiveEntitlements.Of(new Dictionary<string, EntitlementValue>(), [], Now).CheckFeature("roles.custom");

        feature.Error.Code.ShouldBe(ControlPlaneErrorCodes.PlanFeatureUnavailable);
        feature.Error.Entitlement.ShouldBe("roles.custom");
    }

    [Fact]
    public void CheckLimit_AtTheLimit_IsAConflictWithTheKey()
    {
        var limit = EffectiveEntitlements.Of(Pro, [], Now).CheckLimit("roles.custom.max", 20);

        limit.Error.Code.ShouldBe(ControlPlaneErrorCodes.PlanLimitReached);
        limit.Error.Entitlement.ShouldBe("roles.custom.max");
    }

    [Fact]
    public void CheckLimit_UnderTheLimit_Passes() => EffectiveEntitlements.Of(Pro, [], Now).CheckLimit("roles.custom.max", 19).IsSuccess.ShouldBeTrue();
}
