using ControlPlane.Domain.Plans;

namespace ControlPlane.UnitTests.Plans;

// A preview lists every key a change would put the tenant over, with its count and the new value, and how far over it goes: a limit
// under the count, and a feature that turns off while the tenant uses it. An override that still applies keeps its key out.
public sealed class PlanPreviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly EntitlementUsage Usage = new(CustomRoles: 14, Members: 8, PendingInvitations: 1);

    [Fact]
    public void Preview_ALimitUnderTheCount_ListsTheKeyWithItsCountAndHowFarOver()
    {
        var plan = new Dictionary<string, EntitlementValue>
        {
            ["roles.custom"] = EntitlementValue.On,
            ["roles.custom.max"] = EntitlementValue.Limit(20),
            ["members.max"] = EntitlementValue.Limit(5),
        };

        var preview = PlanPreview.Of(plan, [], Usage, Now);

        preview.Excesses.ShouldBe([new Excess("members.max", 9, EntitlementValue.Limit(5), 4)]);
        preview.Furthest.ShouldBe(4);
    }

    [Fact]
    public void Preview_AFeatureTurnedOffWhileInUse_ListsTheFeatureWithItsCount()
    {
        var plan = new Dictionary<string, EntitlementValue> { ["roles.custom.max"] = EntitlementValue.Limit(20), ["members.max"] = EntitlementValue.Unlimited };

        var preview = PlanPreview.Of(plan, [], Usage, Now);

        preview.Excesses.ShouldBe([new Excess("roles.custom", 14, EntitlementValue.Off, 14)]);
    }

    [Fact]
    public void Preview_AnOverrideThatStillApplies_KeepsItsKeyOutAndNamesIt()
    {
        var plan = new Dictionary<string, EntitlementValue> { ["roles.custom"] = EntitlementValue.On, ["roles.custom.max"] = EntitlementValue.Limit(20) };
        OverrideValue[] overrides = [new("members.max", EntitlementValue.Unlimited, Now.AddDays(1)), new("roles.custom", EntitlementValue.Off, Now.AddDays(-1))];

        var preview = PlanPreview.Of(plan, overrides, Usage, Now);

        preview.Excesses.ShouldBeEmpty();
        preview.OverriddenKeys.ShouldBe(["members.max"]);
    }

    [Fact]
    public void Preview_EverythingWithin_ListsNothing()
    {
        var plan = new Dictionary<string, EntitlementValue>
        {
            ["roles.custom"] = EntitlementValue.On,
            ["roles.custom.max"] = EntitlementValue.Limit(14),
            ["members.max"] = EntitlementValue.Limit(9),
        };

        PlanPreview.Of(plan, [], Usage, Now).Excesses.ShouldBeEmpty();
    }
}
