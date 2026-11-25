using ControlPlane.Domain.Plans;

namespace ControlPlane.UnitTests.Plans;

public sealed class EntitlementKeysTests
{
    // D1: the keys are declared once, each with its kind, in FeatureKeys and LimitKeys. A product adds its own beside them (X1), so the
    // test names the template's keys only (X4).
    [Fact]
    public void ListKeys_TheCodesList_HoldsEachTemplateKeyWithItsKind()
    {
        var templateKeys = new Dictionary<string, EntitlementKind>
        {
            ["roles.custom"] = EntitlementKind.Feature,
            ["roles.custom.max"] = EntitlementKind.Limit,
            ["members.max"] = EntitlementKind.Limit,
            ["audit.retention.days"] = EntitlementKind.Limit,
        };

        templateKeys.ShouldBeSubsetOf(EntitlementKeys.All);
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

    // E1: a limit names the feature it depends on, once, in its declaration in LimitKeys.
    [Fact]
    public void ListDependencies_TheCodesList_HoldsEachDependentLimitWithItsFeature()
    {
        EntitlementKeys.Dependencies.ShouldBe(new Dictionary<string, string> { ["roles.custom.max"] = "roles.custom" }, ignoreOrder: true);
    }

    [Fact]
    public void ListDependencies_EveryDependentLimit_NamesAFeatureOfTheList()
    {
        var wrong = EntitlementKeys.Dependencies.Where(pair => EntitlementKeys.KindOf(pair.Key) != EntitlementKind.Limit || EntitlementKeys.KindOf(pair.Value) != EntitlementKind.Feature);

        wrong.ShouldBeEmpty();
    }

    [Fact]
    public void FeatureOf_ALimitWithoutAFeature_IsNone() => EntitlementKeys.FeatureOf("members.max").ShouldBeNull();
}
