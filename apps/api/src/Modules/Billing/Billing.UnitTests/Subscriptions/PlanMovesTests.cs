using Billing.Domain.Subscriptions;

namespace Billing.UnitTests.Subscriptions;

// B9: an upgrade and a move from monthly to yearly apply at once; a lower plan and a move from yearly to monthly at the period's end; the
// default plan is the cancellation. "Lower" compares what both plans cost for the seated members in the same interval.
public sealed class PlanMovesTests
{
    private static readonly Guid Team = Guid.CreateVersion7();

    private static readonly Guid Business = Guid.CreateVersion7();

    private static readonly Guid Free = Guid.CreateVersion7();

    [Fact]
    public void Of_AMoreExpensivePlanInTheSameInterval_AppliesAtOnce() =>
        PlanMoves.Of(Team, BillingInterval.Monthly, false, 10000, Business, BillingInterval.Monthly, false, 17900).ShouldBe(PlanMove.Now);

    [Fact]
    public void Of_ACheaperPlanInTheSameInterval_AppliesAtThePeriodsEnd() =>
        PlanMoves.Of(Business, BillingInterval.Monthly, false, 17900, Team, BillingInterval.Monthly, false, 10000).ShouldBe(PlanMove.AtPeriodEnd);

    [Fact]
    public void Of_MonthlyToYearly_AppliesAtOnce() =>
        PlanMoves.Of(Team, BillingInterval.Monthly, false, 10000, Team, BillingInterval.Yearly, false, 100000).ShouldBe(PlanMove.Now);

    [Fact]
    public void Of_YearlyToMonthlyOfAMoreExpensivePlan_AppliesAtTheEndOfTheYear() =>
        PlanMoves.Of(Team, BillingInterval.Yearly, false, 100000, Business, BillingInterval.Monthly, false, 17900).ShouldBe(PlanMove.AtPeriodEnd);

    [Fact]
    public void Of_TheDefaultPlan_IsTheCancellation() =>
        PlanMoves.Of(Team, BillingInterval.Monthly, false, 10000, Free, BillingInterval.Monthly, true, 0).ShouldBe(PlanMove.Cancel);

    [Fact]
    public void Of_TheDefaultPlanWhenTheCancellationIsAskedAlready_IsNoMove() =>
        PlanMoves.Of(Team, BillingInterval.Monthly, true, 10000, Free, BillingInterval.Monthly, true, 0).ShouldBeNull();

    [Fact]
    public void Of_ThePlanAndIntervalItPaysFor_IsNoMove() =>
        PlanMoves.Of(Team, BillingInterval.Yearly, false, 100000, Team, BillingInterval.Yearly, false, 100000).ShouldBeNull();
}
