using ControlPlane.Domain.Plans;

namespace ControlPlane.UnitTests.Plans;

public sealed class EntitlementKeysTests
{
    // D1: the keys are declared once, each with its kind, in FeatureKeys and LimitKeys.
    [Fact]
    public void ListKeys_TheCodesList_HoldsEachKeyWithItsKind()
    {
        EntitlementKeys.All.ShouldBe(
            new Dictionary<string, EntitlementKind>
            {
                ["roles.custom"] = EntitlementKind.Feature,
                ["roles.custom.max"] = EntitlementKind.Limit,
                ["members.max"] = EntitlementKind.Limit,
            },
            ignoreOrder: true);
    }

    // A key is lowercase and dotted, its area first, like a permission name.
    [Fact]
    public void NameKeys_EveryKey_IsLowercaseAndDotted()
    {
        var misnamed = EntitlementKeys.All.Keys.Where(key => !System.Text.RegularExpressions.Regex.IsMatch(key, "^[a-z][a-z_]*(\\.[a-z][a-z_]*)+$"));

        misnamed.ShouldBeEmpty();
    }

    [Fact]
    public void KindOf_AFeatureKey_IsFeature() => EntitlementKeys.KindOf("roles.custom").ShouldBe(EntitlementKind.Feature);

    [Fact]
    public void KindOf_ALimitKey_IsLimit() => EntitlementKeys.KindOf("members.max").ShouldBe(EntitlementKind.Limit);

    [Fact]
    public void KindOf_AKeyNotOnTheList_IsNone() => EntitlementKeys.KindOf("roles.unknown").ShouldBeNull();
}
