using Billing.Domain.Subscriptions;
using SharedKernel;

namespace Billing.UnitTests.Subscriptions;

// A subscription takes the provider's state: the state it has changes nothing (I1), and only an active one pays for its plan.
public sealed class SubscriptionTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly SubscriptionState Active =
        new("cus_1", "sub_1", Guid.CreateVersion7(), BillingInterval.Monthly, SubscriptionStatus.Active, At.AddMonths(1), CancelAtPeriodEnd: false);

    [Fact]
    public void Sync_TheStateItHas_ChangesNothing()
    {
        var subscription = Subscription.Start(Active, At);

        var change = subscription.Sync(Active, At.AddMinutes(1));

        (change, subscription.UpdatedAt).ShouldBe((StateChange.Unchanged, At));
    }

    [Fact]
    public void Sync_AnotherState_TakesIt()
    {
        var subscription = Subscription.Start(Active, At);

        var change = subscription.Sync(Active with { Status = SubscriptionStatus.PastDue }, At.AddMinutes(1));

        (change, subscription.Status, subscription.PaysForItsPlan, subscription.UpdatedAt).ShouldBe((StateChange.Changed, SubscriptionStatus.PastDue, false, At.AddMinutes(1)));
    }

    // B5: subscription A ended and B replaced it; a late read of A, which Stripe may send after B's, keeps B.
    [Fact]
    public void Sync_ALateReadOfAnEndedSubscriptionAfterItsSuccessor_KeepsTheSuccessor()
    {
        var subscription = Subscription.Start(Active with { SubscriptionId = "sub_b" }, At);

        var change = subscription.Sync(Active with { SubscriptionId = "sub_a", Status = SubscriptionStatus.Canceled }, At.AddMinutes(1));

        (change, subscription.SubscriptionId, subscription.Status).ShouldBe((StateChange.Unchanged, "sub_b", SubscriptionStatus.Active));
    }

    // A new subscription after an ended one takes the row.
    [Fact]
    public void Sync_ANewSubscriptionAfterAnEndedOne_TakesTheRow()
    {
        var subscription = Subscription.Start(Active with { SubscriptionId = "sub_a", Status = SubscriptionStatus.Canceled }, At);

        var change = subscription.Sync(Active with { SubscriptionId = "sub_b" }, At.AddMinutes(1));

        (change, subscription.SubscriptionId, subscription.Status).ShouldBe((StateChange.Changed, "sub_b", SubscriptionStatus.Active));
    }

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Trialing", false)]
    [InlineData("Incomplete", false)]
    [InlineData("Unpaid", false)]
    [InlineData("Canceled", false)]
    public void PaysForItsPlan_EachStatus_OnlyWhileActive(string status, bool pays)
    {
        var subscription = Subscription.Start(Active with { Status = Enum.Parse<SubscriptionStatus>(status) }, At);

        subscription.PaysForItsPlan.ShouldBe(pays);
    }
}
