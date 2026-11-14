using ControlPlane.Domain;
using ControlPlane.Domain.FeatureFlags;
using SharedKernel;

namespace ControlPlane.UnitTests.FeatureFlags;

public sealed class FeatureFlagTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Admin = Guid.CreateVersion7();

    private static readonly Guid Acme = Guid.CreateVersion7();

    [Fact]
    public void AddFeatureFlag_AReleaseFlag_StartsOffForEveryoneWithItsDayAndCommit()
    {
        var (flag, change) = FeatureFlag.Add(Release(), Now, "7c41d09");

        (flag.DefaultValue, flag.EveryoneElse, flag.Percentage, flag.ListedCount, flag.AddedAt, flag.AddedRevision, change.Kind, change.Revision)
            .ShouldBe((false, false, 0, 0, Now, "7c41d09", FeatureFlagChangeKind.AddedToCode, "7c41d09"));
    }

    [Fact]
    public void AddFeatureFlag_AKillSwitch_StartsOnForEveryone()
    {
        var (flag, _) = FeatureFlag.Add(new FeatureFlagDefinition("emails.send", FeatureFlagType.KillSwitch, "Sends the emails."), Now, null);

        (flag.DefaultValue, flag.EveryoneElse).ShouldBe((true, true));
    }

    [Fact]
    public void AddTenant_NotListed_CountsItAndRecordsTheChangeByTheAdmin()
    {
        var flag = Added();

        var change = flag.AddTenant(Acme, listed: false, Admin, Now.AddHours(1)).Value!;

        (flag.ListedCount, flag.LastChangedBy, flag.LastChangedAt, change.Kind, change.TenantId, change.ActorId)
            .ShouldBe((1, (Guid?)Admin, Now.AddHours(1), FeatureFlagChangeKind.Listed, (Guid?)Acme, (Guid?)Admin));
    }

    [Fact]
    public void AddTenant_ListedAlready_ChangesNothing()
    {
        var flag = Added();

        flag.AddTenant(Acme, listed: true, Admin, Now).Value.ShouldBeNull();
        flag.ListedCount.ShouldBe(0);
    }

    [Fact]
    public void RemoveTenant_Listed_TakesItFromTheCount()
    {
        var flag = Added();
        flag.AddTenant(Acme, listed: false, Admin, Now);

        var change = flag.RemoveTenant(Acme, listed: true, Admin, Now).Value!;

        (flag.ListedCount, change.Kind).ShouldBe((0, FeatureFlagChangeKind.Unlisted));
    }

    [Fact]
    public void RemoveTenant_NotListed_ChangesNothing() => Added().RemoveTenant(Acme, listed: false, Admin, Now).Value.ShouldBeNull();

    [Fact]
    public void SetPercentage_AnotherValue_RecordsFromAndTo()
    {
        var flag = Added();

        var change = flag.SetPercentage(10, Admin, Now).Value!;

        (flag.Percentage, change.Kind, change.From, change.To).ShouldBe((10, FeatureFlagChangeKind.PercentageChanged, "0", "10"));
    }

    [Fact]
    public void SetPercentage_TheSameValue_ChangesNothing() => Added().SetPercentage(0, Admin, Now).Value.ShouldBeNull();

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void SetPercentage_OutsideNoughtToAHundred_IsInvalid(int percentage)
    {
        var set = Added().SetPercentage(percentage, Admin, Now);

        (set.Error.Code, set.Error.Type).ShouldBe((ControlPlaneErrorCodes.FeatureFlagPercentageInvalid, ErrorType.Validation));
    }

    [Fact]
    public void SetEveryoneElse_On_RecordsOffToOn()
    {
        var flag = Added();

        var change = flag.SetEveryoneElse(true, Admin, Now).Value!;

        (flag.EveryoneElse, change.Kind, change.From, change.To).ShouldBe((true, FeatureFlagChangeKind.EveryoneElseChanged, "off", "on"));
    }

    [Fact]
    public void SetEveryoneElse_TheSameValue_ChangesNothing() => Added().SetEveryoneElse(false, Admin, Now).Value.ShouldBeNull();

    [Fact]
    public void ChangeRule_AFlagRemovedFromCode_IsAConflictAndChangesNothing()
    {
        var flag = Added();
        flag.RemoveFromCode(Now, "abc1234");

        var changes = new[]
        {
            flag.AddTenant(Acme, false, Admin, Now),
            flag.RemoveTenant(Acme, true, Admin, Now),
            flag.SetPercentage(50, Admin, Now),
            flag.SetEveryoneElse(true, Admin, Now),
        };

        changes.Select(change => change.Error.Code).ShouldAllBe(code => code == ControlPlaneErrorCodes.FeatureFlagRemovedFromCode);
        (flag.ListedCount, flag.Percentage, flag.EveryoneElse).ShouldBe((0, 0, false));
    }

    [Fact]
    public void RemoveFromCode_Twice_KeepsTheFirstDayAndCommit()
    {
        var flag = Added();
        flag.RemoveFromCode(Now, "first00");

        var second = flag.RemoveFromCode(Now.AddDays(1), "second0");

        (second, flag.RemovedAt, flag.RemovedRevision).ShouldBe((null, (DateTimeOffset?)Now, "first00"));
    }

    [Fact]
    public void CleanupAt_AReleaseFlagOnForEveryoneFor30Days_IsReadyToRemove()
    {
        var flag = Added();
        flag.SetEveryoneElse(true, Admin, Now);

        (flag.CleanupAt(Now.AddDays(30).AddSeconds(-1)), flag.CleanupAt(Now.AddDays(30))).ShouldBe((null, FeatureFlagCleanup.ReadyToRemove));
    }

    [Fact]
    public void CleanupAt_AReleaseFlagAtAHundredPercentFor30Days_IsReadyToRemove()
    {
        var flag = Added();
        flag.SetPercentage(100, Admin, Now);

        flag.CleanupAt(Now.AddDays(30)).ShouldBe(FeatureFlagCleanup.ReadyToRemove);
    }

    [Fact]
    public void CleanupAt_AKillSwitchOnForEveryone_IsNone()
    {
        var (flag, _) = FeatureFlag.Add(new FeatureFlagDefinition("emails.send", FeatureFlagType.KillSwitch, "Sends the emails."), Now, null);

        flag.CleanupAt(Now.AddDays(90)).ShouldBeNull();
    }

    [Fact]
    public void CleanupAt_AFlagOffForEveryOrganisationFor30DaysSinceItWasAdded_IsUnused() =>
        Added().CleanupAt(Now.AddDays(30)).ShouldBe(FeatureFlagCleanup.Unused);

    [Fact]
    public void CleanupAt_AKillSwitchOffForEveryOrganisationFor30Days_IsUnused()
    {
        var (flag, _) = FeatureFlag.Add(new FeatureFlagDefinition("emails.send", FeatureFlagType.KillSwitch, "Sends the emails."), Now, null);
        flag.SetEveryoneElse(false, Admin, Now);

        flag.CleanupAt(Now.AddDays(30)).ShouldBe(FeatureFlagCleanup.Unused);
    }

    [Fact]
    public void CleanupAt_AListedOrganisation_IsNone()
    {
        var flag = Added();
        flag.AddTenant(Acme, false, Admin, Now);

        flag.CleanupAt(Now.AddDays(60)).ShouldBeNull();
    }

    [Fact]
    public void CleanupAt_AChangeThatKeepsTheFlagOnForEveryone_CountsFromTheFirstMoment()
    {
        var flag = Added();
        flag.SetEveryoneElse(true, Admin, Now);
        flag.SetPercentage(20, Admin, Now.AddDays(20));

        (flag.StateSince, flag.CleanupAt(Now.AddDays(30))).ShouldBe((Now, FeatureFlagCleanup.ReadyToRemove));
    }

    [Fact]
    public void CleanupAt_AFlagThatTurnedOffAgain_CountsFromItsLastTurn()
    {
        var flag = Added();
        flag.SetPercentage(10, Admin, Now.AddDays(10));
        flag.SetPercentage(0, Admin, Now.AddDays(20));

        (flag.StateSince, flag.CleanupAt(Now.AddDays(40))).ShouldBe((Now.AddDays(20), (FeatureFlagCleanup?)null));
    }

    [Fact]
    public void CleanupAt_AFlagRemovedFromCode_IsNone()
    {
        var flag = Added();
        flag.RemoveFromCode(Now, null);

        flag.CleanupAt(Now.AddDays(60)).ShouldBeNull();
    }

    [Fact]
    public void Describe_AnotherDescription_TakesIt()
    {
        var flag = Added();

        (flag.Describe("Edits many roles at once."), flag.Description).ShouldBe((StateChange.Changed, "Edits many roles at once."));
    }

    private static FeatureFlagDefinition Release() => new("roles.bulk_edit", FeatureFlagType.Release, "Edits several roles at once.");

    private static FeatureFlag Added() => FeatureFlag.Add(Release(), Now, "7c41d09").Flag;
}
