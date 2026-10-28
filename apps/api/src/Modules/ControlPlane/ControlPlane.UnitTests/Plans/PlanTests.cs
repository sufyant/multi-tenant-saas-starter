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

    [Fact]
    public void CreatePlan_ANameWithSpacesAround_KeepsItTrimmed() => Plan.Create(Guid.NewGuid(), "  Pro  ", Values, false, Now).Value.Name.ShouldBe("Pro");

    [Fact]
    public void CreatePlan_AKeyWithoutAValue_HasNoRowForIt() =>
        Plan.Create(Guid.NewGuid(), "Pro", Values, false, Now).Value.ValueMap.Keys.ShouldBe(["roles.custom", "roles.custom.max"], ignoreOrder: true);

    [Theory]
    [InlineData("   ")]
    [InlineData("a name that is one character longer than the limit of a plan's name, which is one hundred characters.")]
    public void CreatePlan_ANameOutOfItsLength_IsInvalid(string name) =>
        Plan.Create(Guid.NewGuid(), name, Values, false, Now).Error.Code.ShouldBe(ControlPlaneErrorCodes.PlanNameInvalid);

    [Fact]
    public void CreatePlan_ATakenName_IsAConflict()
    {
        var created = Plan.Create(Guid.NewGuid(), "Pro", Values, nameTaken: true, Now);

        (created.Error.Code, created.Error.Type).ShouldBe((ControlPlaneErrorCodes.PlanNameTaken, ErrorType.Conflict));
    }

    [Fact]
    public void CreatePlan_AValueOfTheWrongKind_IsInvalidWithTheKey()
    {
        var created = Plan.Create(Guid.NewGuid(), "Pro", new Dictionary<string, EntitlementValue> { ["members.max"] = EntitlementValue.On }, false, Now);

        created.Error.Entitlement.ShouldBe("members.max");
    }

    [Fact]
    public void EditPlan_AKeyLeftOut_LosesItsValue()
    {
        var plan = Plan.Create(Guid.NewGuid(), "Pro", Values, false, Now).Value;

        plan.Edit("Pro", new Dictionary<string, EntitlementValue> { ["roles.custom"] = EntitlementValue.Off }, false);

        plan.ValueMap.ShouldBe(new Dictionary<string, EntitlementValue> { ["roles.custom"] = EntitlementValue.Off });
    }

    [Fact]
    public void EditPlan_NewValues_AnswersTheOldOnes()
    {
        var plan = Plan.Create(Guid.NewGuid(), "Pro", Values, false, Now).Value;

        var edit = plan.Edit("Business", new Dictionary<string, EntitlementValue> { ["members.max"] = EntitlementValue.Unlimited }, false);

        edit.Value.ShouldBe(new PlanEdit("Pro", Values), new PlanEditComparer());
        plan.ValueMap.ShouldBe(new Dictionary<string, EntitlementValue> { ["members.max"] = EntitlementValue.Unlimited });
    }

    [Fact]
    public void EditPlan_ATakenName_ChangesNothing()
    {
        var plan = Plan.Create(Guid.NewGuid(), "Pro", Values, false, Now).Value;

        var edit = plan.Edit("Free", new Dictionary<string, EntitlementValue>(), nameTaken: true);

        edit.Error.Code.ShouldBe(ControlPlaneErrorCodes.PlanNameTaken);
        (plan.Name, plan.ValueMap.Count).ShouldBe(("Pro", 2));
    }

    private sealed class PlanEditComparer : IEqualityComparer<PlanEdit>
    {
        public bool Equals(PlanEdit? x, PlanEdit? y) =>
            x!.OldName == y!.OldName && x.OldValues.OrderBy(value => value.Key).SequenceEqual(y.OldValues.OrderBy(value => value.Key));

        public int GetHashCode(PlanEdit obj) => obj.OldName.GetHashCode(StringComparison.Ordinal);
    }
}
