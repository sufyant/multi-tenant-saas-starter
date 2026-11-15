using ControlPlane.Domain;
using ControlPlane.Domain.Plans;
using SharedKernel;

namespace ControlPlane.UnitTests.Plans;

public sealed class PlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, EntitlementValue> Values = new()
    {
        ["roles.custom"] = EntitlementValue.On,
        ["roles.custom.max"] = EntitlementValue.Limit(20),
    };

    // The tests' own meter: the template defines none (MeterCatalog.Template).
    private static readonly MeterCatalog Catalog = new([new MeterDefinition("ai.requests", "AI requests the organisation's members make.")]);

    private static readonly MeterSetting AiRequests = new(Quota: 10_000, MeterOverage.Charge, UnitPrice: 100, UnitSize: 1_000);

    [Fact]
    public void CreatePlan_ANameWithSpacesAround_KeepsItTrimmed() => Plan.Create(Guid.NewGuid(), "  Pro  ", Content(Values), Catalog, false, Now).Value.Name.ShouldBe("Pro");

    [Fact]
    public void CreatePlan_AKeyWithoutAValue_HasNoRowForIt() =>
        Plan.Create(Guid.NewGuid(), "Pro", Content(Values), Catalog, false, Now).Value.ValueMap.Keys.ShouldBe(["roles.custom", "roles.custom.max"], ignoreOrder: true);

    [Theory]
    [InlineData("   ")]
    [InlineData("a name that is one character longer than the limit of a plan's name, which is one hundred characters.")]
    public void CreatePlan_ANameOutOfItsLength_IsInvalid(string name) =>
        Plan.Create(Guid.NewGuid(), name, Content(Values), Catalog, false, Now).Error.Code.ShouldBe(ControlPlaneErrorCodes.PlanNameInvalid);

    [Fact]
    public void CreatePlan_ATakenName_IsAConflict()
    {
        var created = Plan.Create(Guid.NewGuid(), "Pro", Content(Values), Catalog, nameTaken: true, Now);

        (created.Error.Code, created.Error.Type).ShouldBe((ControlPlaneErrorCodes.PlanNameTaken, ErrorType.Conflict));
    }

    [Fact]
    public void CreatePlan_AValueOfTheWrongKind_IsInvalidWithTheKey()
    {
        var created = Plan.Create(Guid.NewGuid(), "Pro", Content(new Dictionary<string, EntitlementValue> { ["members.max"] = EntitlementValue.On }), Catalog, false, Now);

        created.Error.Entitlement.ShouldBe("members.max");
    }

    [Fact]
    public void EditPlan_AKeyLeftOut_LosesItsValue()
    {
        var plan = Plan.Create(Guid.NewGuid(), "Pro", Content(Values), Catalog, false, Now).Value;

        plan.Edit("Pro", Content(new Dictionary<string, EntitlementValue> { ["roles.custom"] = EntitlementValue.Off }), Catalog, false);

        plan.ValueMap.ShouldBe(new Dictionary<string, EntitlementValue> { ["roles.custom"] = EntitlementValue.Off });
    }

    [Fact]
    public void EditPlan_NewValues_AnswersTheOldOnes()
    {
        var plan = Plan.Create(Guid.NewGuid(), "Pro", Content(Values), Catalog, false, Now).Value;

        var edit = plan.Edit("Business", Content(new Dictionary<string, EntitlementValue> { ["members.max"] = EntitlementValue.Unlimited }), Catalog, false);

        edit.Value.ShouldBe(new PlanEdit("Pro", Content(Values)), new PlanEditComparer());
        plan.ValueMap.ShouldBe(new Dictionary<string, EntitlementValue> { ["members.max"] = EntitlementValue.Unlimited });
    }

    [Fact]
    public void EditPlan_ATakenName_ChangesNothing()
    {
        var plan = Plan.Create(Guid.NewGuid(), "Pro", Content(Values), Catalog, false, Now).Value;

        var edit = plan.Edit("Free", Content(new Dictionary<string, EntitlementValue>()), Catalog, nameTaken: true);

        edit.Error.Code.ShouldBe(ControlPlaneErrorCodes.PlanNameTaken);
        (plan.Name, plan.ValueMap.Count).ShouldBe(("Pro", 2));
    }

    // Step 40: a plan has a base fee and a fee per person, monthly and yearly, in minor units; each can be 0.
    [Fact]
    public void CreatePlan_PricesAndAMeter_KeepsThem()
    {
        var prices = new PlanPrices(BaseMonthly: 0, PerPersonMonthly: 1_000, BaseYearly: 0, PerPersonYearly: 10_000);

        var plan = Plan.Create(Guid.NewGuid(), "Team", new PlanContent(Values, prices, new Dictionary<string, MeterSetting> { ["ai.requests"] = AiRequests }), Catalog, false, Now).Value;

        (plan.Prices, plan.MeterMap["ai.requests"]).ShouldBe((prices, AiRequests));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(PlanPrices.Maximum + 1)]
    public void CreatePlan_APriceOutOfRange_IsInvalid(long price)
    {
        var created = Plan.Create(Guid.NewGuid(), "Team", new PlanContent(Values, new PlanPrices(0, price, 0, 0), new Dictionary<string, MeterSetting>()), Catalog, false, Now);

        (created.Error.Code, created.Error.Type).ShouldBe((ControlPlaneErrorCodes.PlanPriceInvalid, ErrorType.Validation));
    }

    [Fact]
    public void CreatePlan_AMeterTheCodeDoesNotDefine_IsInvalid()
    {
        var created = Plan.Create(Guid.NewGuid(), "Team", new PlanContent(Values, PlanPrices.Zero, new Dictionary<string, MeterSetting> { ["storage.bytes"] = AiRequests }), Catalog, false, Now);

        created.Error.Code.ShouldBe(ControlPlaneErrorCodes.PlanMeterInvalid);
    }

    [Theory]
    [InlineData(-1, 100, 1_000)]
    [InlineData(10_000, -1, 1_000)]
    [InlineData(10_000, 100, 0)]
    public void CreatePlan_AMeterSettingOutOfRange_IsInvalid(long quota, long unitPrice, long unitSize)
    {
        var meters = new Dictionary<string, MeterSetting> { ["ai.requests"] = new(quota, MeterOverage.Charge, unitPrice, unitSize) };

        var created = Plan.Create(Guid.NewGuid(), "Team", new PlanContent(Values, PlanPrices.Zero, meters), Catalog, false, Now);

        created.Error.Code.ShouldBe(ControlPlaneErrorCodes.PlanMeterInvalid);
    }

    // An edit gives the plan exactly the meters it sends, as it does the values: a meter left out is no longer on the plan.
    [Fact]
    public void EditPlan_AMeterLeftOutAndNewPrices_KeepsTheEditAndAnswersTheOldOnes()
    {
        var before = new PlanContent(Values, new PlanPrices(0, 1_000, 0, 10_000), new Dictionary<string, MeterSetting> { ["ai.requests"] = AiRequests });
        var plan = Plan.Create(Guid.NewGuid(), "Team", before, Catalog, false, Now).Value;

        var edit = plan.Edit("Team", new PlanContent(Values, new PlanPrices(0, 1_200, 0, 12_000), new Dictionary<string, MeterSetting>()), Catalog, false);

        edit.Value.ShouldBe(new PlanEdit("Team", before), new PlanEditComparer());
        (plan.Prices, plan.MeterMap.Count).ShouldBe((new PlanPrices(0, 1_200, 0, 12_000), 0));
    }

    private static PlanContent Content(IReadOnlyDictionary<string, EntitlementValue> values) => new(values, PlanPrices.Zero, new Dictionary<string, MeterSetting>());

    private sealed class PlanEditComparer : IEqualityComparer<PlanEdit>
    {
        public bool Equals(PlanEdit? x, PlanEdit? y) =>
            x!.OldName == y!.OldName
            && x.Old.Values.OrderBy(value => value.Key).SequenceEqual(y.Old.Values.OrderBy(value => value.Key))
            && x.Old.Prices == y.Old.Prices
            && x.Old.Meters.OrderBy(meter => meter.Key).SequenceEqual(y.Old.Meters.OrderBy(meter => meter.Key));

        public int GetHashCode(PlanEdit obj) => obj.OldName.GetHashCode(StringComparison.Ordinal);
    }
}
