using ControlPlane.Domain;
using ControlPlane.Domain.Plans;
using SharedKernel;

namespace ControlPlane.UnitTests.Plans;

public sealed class DefaultPlanTests
{
    private static readonly Guid Free = Guid.NewGuid();

    private static readonly Guid Pro = Guid.NewGuid();

    [Fact]
    public void MarkDefault_AnotherPlan_MovesTheMark()
    {
        var mark = new DefaultPlan(Free);

        var marked = mark.Mark(Pro, isDefault: true);

        (marked.Value, mark.PlanId).ShouldBe((StateChange.Changed, Pro));
    }

    [Fact]
    public void MarkDefault_TheDefaultPlanAgain_ChangesNothing() => new DefaultPlan(Free).Mark(Free, isDefault: true).Value.ShouldBe(StateChange.Unchanged);

    [Fact]
    public void UnmarkDefault_TheDefaultPlan_IsAConflictAndKeepsTheMark()
    {
        var mark = new DefaultPlan(Free);

        var unmarked = mark.Mark(Free, isDefault: false);

        (unmarked.Error.Code, mark.PlanId).ShouldBe((ControlPlaneErrorCodes.PlanDefaultRequired, Free));
    }

    [Fact]
    public void UnmarkDefault_APlanThatIsNotTheDefault_ChangesNothing() => new DefaultPlan(Free).Mark(Pro, isDefault: false).Value.ShouldBe(StateChange.Unchanged);
}
