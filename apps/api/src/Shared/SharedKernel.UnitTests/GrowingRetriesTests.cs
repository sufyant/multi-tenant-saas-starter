namespace SharedKernel.UnitTests;

public class GrowingRetriesTests
{
    [Fact]
    public void SplitRetries_PausesBelowAndFromAMinute_AreInlineThenScheduled()
    {
        var retries = GrowingRetries.Of([TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(59), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)]);

        retries.Inline.ShouldBe([TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(59)]);
        retries.Scheduled.ShouldBe([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)]);
    }

    [Fact]
    public void CheckRetries_PausesThatGrow_AreValid()
    {
        GrowingRetries.AreValid([TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1)]).ShouldBeTrue();
    }

    [Theory]
    [InlineData(new double[0])]
    [InlineData(new double[] { 0, 60 })]
    [InlineData(new double[] { 60, 60 })]
    [InlineData(new double[] { 60, 10 })]
    public void CheckRetries_NoPausesAZeroPauseOrPausesThatDoNotGrow_AreInvalid(double[] seconds)
    {
        GrowingRetries.AreValid([.. seconds.Select(TimeSpan.FromSeconds)]).ShouldBeFalse();
    }
}
