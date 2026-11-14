using Notifications.Application.Notifications;

namespace Notifications.UnitTests.Notifications;

// A fan-out to every user goes at a pace: the first deliveries of each second at once, the later ones a second later each, so one event
// never sends more emails a second than the setting allows (Resend, "Rate limit").
public sealed class NotificationFanOutPaceTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(999, 199)]
    public void DelayDelivery_APaceOfFive_WaitsAWholeSecondForEachFive(int index, int seconds) =>
        NotificationFanOut.DelayOf(index, deliveriesPerSecond: 5).ShouldBe(TimeSpan.FromSeconds(seconds));

    [Fact]
    public void DelayDelivery_NoPace_SendsEveryDeliveryAtOnce() => NotificationFanOut.DelayOf(999, deliveriesPerSecond: null).ShouldBe(TimeSpan.Zero);
}
