using ControlPlane.Domain.Plans;

namespace ControlPlane.UnitTests.Plans;

// K14, R19: the floor and the hold are 30 days, and a period gets shorter when the new value deletes an entry that the value before kept.
// Unlimited keeps every entry, so it is the longest period.
public sealed class AuditRetentionTests
{
    [Fact]
    public void ReadTheFloor_TheShortestPeriod_Is30Days() => AuditRetention.Floor.ShouldBe(30);

    [Fact]
    public void ReadTheGrace_TheHold_Is30Days() => AuditRetention.Grace.ShouldBe(30);

    [Fact]
    public void ShorteningOf_FromUnlimitedToAYear_IsFromNoLimitToAYear() =>
        AuditRetention.ShorteningOf(EntitlementValue.Unlimited, EntitlementValue.Limit(365)).ShouldBe((null, 365));

    [Fact]
    public void ShorteningOf_FromAYearToNinetyDays_IsFromAYearToNinetyDays() =>
        AuditRetention.ShorteningOf(EntitlementValue.Limit(365), EntitlementValue.Limit(90)).ShouldBe((365, 90));

    [Fact]
    public void ShorteningOf_FromNinetyDaysToAYear_IsNone() =>
        AuditRetention.ShorteningOf(EntitlementValue.Limit(90), EntitlementValue.Limit(365)).ShouldBeNull();

    [Fact]
    public void ShorteningOf_TheSamePeriod_IsNone() =>
        AuditRetention.ShorteningOf(EntitlementValue.Limit(365), EntitlementValue.Limit(365)).ShouldBeNull();

    [Fact]
    public void ShorteningOf_FromAYearToUnlimited_IsNone() =>
        AuditRetention.ShorteningOf(EntitlementValue.Limit(365), EntitlementValue.Unlimited).ShouldBeNull();
}
