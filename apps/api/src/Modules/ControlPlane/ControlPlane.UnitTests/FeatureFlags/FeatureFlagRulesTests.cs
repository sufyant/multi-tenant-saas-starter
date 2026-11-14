using ControlPlane.Domain.FeatureFlags;

namespace ControlPlane.UnitTests.FeatureFlags;

public sealed class FeatureFlagRulesTests
{
    private const string Key = "roles.bulk_edit";

    // 200 organisations whose ids never change, so the counts below are the same in every run.
    private static readonly Guid[] Tenants = [.. Enumerable.Range(1, 200).Select(number => new Guid($"00000000-0000-7000-8000-{number:D12}"))];

    [Fact]
    public void ReasonFor_AListedOrganisationWithEveryRuleOn_IsListed() =>
        new FeatureFlagRules(Key, true, 100, true).ReasonFor(Tenants[0]).ShouldBe(FeatureFlagReason.Listed);

    [Fact]
    public void ReasonFor_AHundredPercent_IsThePercentage() =>
        new FeatureFlagRules(Key, false, 100, true).ReasonFor(Tenants[0]).ShouldBe(FeatureFlagReason.Percentage);

    [Fact]
    public void ReasonFor_NoPercentageAndEveryoneElseOn_IsEveryone() =>
        new FeatureFlagRules(Key, false, 0, true).ReasonFor(Tenants[0]).ShouldBe(FeatureFlagReason.Everyone);

    [Fact]
    public void ReasonFor_EveryRuleOff_IsNone() => new FeatureFlagRules(Key, false, 0, false).ReasonFor(Tenants[0]).ShouldBeNull();

    [Fact]
    public void Bucket_TheSameFlagAndOrganisation_IsTheSameEveryTime() =>
        FeatureFlagBucket.Of(Key, Tenants[7]).ShouldBe(FeatureFlagBucket.Of(Key, Tenants[7]));

    [Fact]
    public void Bucket_EveryOrganisation_IsFromNoughtTo99() =>
        Tenants.Select(tenant => FeatureFlagBucket.Of(Key, tenant)).ShouldAllBe(bucket => bucket >= 0 && bucket < 100);

    // Written out from another implementation: Python's hashlib, sha256(b"roles.bulk_edit:0193a1b2-...") read as a big-endian uint32, mod 100.
    [Fact]
    public void Bucket_AKnownFlagAndOrganisation_IsTheSameInEveryVersion() =>
        FeatureFlagBucket.Of(Key, new Guid("0193a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b")).ShouldBe(65);

    [Fact]
    public void IsOnFor_TheGrowingPercentage_KeepsEveryOrganisationThatWasIn()
    {
        var atTen = Tenants.Where(tenant => new FeatureFlagRules(Key, false, 10, false).IsOnFor(tenant)).ToHashSet();
        var atFifty = Tenants.Where(tenant => new FeatureFlagRules(Key, false, 50, false).IsOnFor(tenant)).ToHashSet();

        atTen.IsSubsetOf(atFifty).ShouldBeTrue();
    }

    [Fact]
    public void IsOnFor_ATenPercent_TurnsOnAboutATenthOfTheOrganisations() =>
        Tenants.Count(tenant => new FeatureFlagRules(Key, false, 10, false).IsOnFor(tenant)).ShouldBeInRange(10, 30);

    [Fact]
    public void IsOnFor_TwoFlagsAtTenPercent_TurnOnOtherOrganisations() =>
        Tenants.Where(tenant => new FeatureFlagRules(Key, false, 10, false).IsOnFor(tenant))
            .SequenceEqual(Tenants.Where(tenant => new FeatureFlagRules("emails.send", false, 10, false).IsOnFor(tenant)))
            .ShouldBeFalse();

    [Fact]
    public void AffectedBy_ListingAnOrganisationTheFlagIsOffFor_CountsOne()
    {
        var before = new FeatureFlagSetting(Key, new HashSet<Guid>(), 0, false);
        var after = before with { Listed = new HashSet<Guid> { Tenants[3] } };

        before.AffectedBy(after, Tenants).ShouldBe(1);
    }

    [Fact]
    public void AffectedBy_ListingAnOrganisationWhileEveryoneElseIsOn_CountsNone()
    {
        var before = new FeatureFlagSetting(Key, new HashSet<Guid>(), 0, true);
        var after = before with { Listed = new HashSet<Guid> { Tenants[3] } };

        before.AffectedBy(after, Tenants).ShouldBe(0);
    }

    [Fact]
    public void AffectedBy_ARaisedPercentage_CountsTheOrganisationsThatComeIn()
    {
        var before = new FeatureFlagSetting(Key, new HashSet<Guid>(), 10, false);
        var after = before with { Percentage = 50 };
        var comeIn = Tenants.Count(tenant => !before.IsOnFor(tenant) && after.IsOnFor(tenant));

        before.AffectedBy(after, Tenants).ShouldBe(comeIn);
    }

    [Fact]
    public void AffectedBy_EveryoneElseTurnedOn_CountsEveryOrganisationThatWasOff()
    {
        var before = new FeatureFlagSetting(Key, new HashSet<Guid> { Tenants[0] }, 0, false);
        var after = before with { EveryoneElse = true };

        before.AffectedBy(after, Tenants).ShouldBe(199);
    }
}
