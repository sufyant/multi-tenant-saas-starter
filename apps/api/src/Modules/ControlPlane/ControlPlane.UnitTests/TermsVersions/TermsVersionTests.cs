using ControlPlane.Domain;
using ControlPlane.Domain.TermsVersions;
using SharedKernel;

namespace ControlPlane.UnitTests.TermsVersions;

public sealed class TermsVersionTests
{
    private const string Link = "https://brand.example/terms/2026-12";

    private static readonly DateTimeOffset Now = new(2026, 11, 14, 22, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Today = new(2026, 11, 14);

    private static readonly DateOnly FirstOfDecember = new(2026, 12, 1);

    private static readonly Guid Admin = Guid.CreateVersion7();

    [Fact]
    public void CreateTermsVersion_TodayAndAnHttpsLink_KeepsTheTrimmedValues()
    {
        var version = TermsVersion.Create(Guid.CreateVersion7(), Today, $" {Link} ", "  Shorter notice period.  ", true, Admin, false, Now).Value;

        version.Values.ShouldBe(new TermsVersionValues(Today, Link, "Shorter notice period.", true));
    }

    [Fact]
    public void CreateTermsVersion_ABlankSummary_KeepsNone() =>
        TermsVersion.Create(Guid.CreateVersion7(), FirstOfDecember, Link, "   ", false, Admin, false, Now).Value.Summary.ShouldBeNull();

    [Fact]
    public void CreateTermsVersion_ADayInThePast_IsInvalid() =>
        TermsVersion.Create(Guid.CreateVersion7(), Today.AddDays(-1), Link, null, true, Admin, false, Now).Error.Code
            .ShouldBe(ControlPlaneErrorCodes.TermsVersionEffectiveDatePast);

    [Theory]
    [InlineData("http://brand.example/terms")]
    [InlineData("/terms")]
    [InlineData("brand.example/terms")]
    [InlineData("mailto:legal@brand.example")]
    [InlineData("")]
    public void CreateTermsVersion_ALinkThatIsNoAbsoluteHttpsUrl_IsInvalid(string link) =>
        TermsVersion.Create(Guid.CreateVersion7(), FirstOfDecember, link, null, true, Admin, false, Now).Error.Code
            .ShouldBe(ControlPlaneErrorCodes.TermsVersionUrlInvalid);

    [Fact]
    public void CreateTermsVersion_AnotherScheduled_IsAConflict()
    {
        var created = TermsVersion.Create(Guid.CreateVersion7(), FirstOfDecember, Link, null, true, Admin, anotherScheduled: true, Now);

        (created.Error.Code, created.Error.Type).ShouldBe((ControlPlaneErrorCodes.TermsVersionScheduledExists, ErrorType.Conflict));
    }

    [Fact]
    public void EditTermsVersion_NobodyAccepted_TakesEveryValueAndReturnsTheOldOnes()
    {
        var version = Scheduled();

        var before = version.Edit(new DateOnly(2026, 12, 15), "https://brand.example/terms/v2", "New", false, accepted: false, Now);

        before.Value.ShouldBe(new TermsVersionValues(FirstOfDecember, Link, null, true));
        version.Values.ShouldBe(new TermsVersionValues(new DateOnly(2026, 12, 15), "https://brand.example/terms/v2", "New", false));
    }

    [Fact]
    public void EditTermsVersion_SomeoneAcceptedAndTheDayAndSummaryChange_TakesThem()
    {
        var version = Scheduled();

        version.Edit(new DateOnly(2026, 12, 15), Link, "New", true, accepted: true, Now).IsSuccess.ShouldBeTrue();

        version.Values.ShouldBe(new TermsVersionValues(new DateOnly(2026, 12, 15), Link, "New", true));
    }

    [Theory]
    [InlineData("https://brand.example/terms/v2", true)]
    [InlineData(Link, false)]
    public void EditTermsVersion_SomeoneAcceptedAndALockedFieldChanges_IsAConflictAndKeepsTheValues(string link, bool requiresAcceptance)
    {
        var version = Scheduled();

        var edited = version.Edit(new DateOnly(2026, 12, 15), link, "New", requiresAcceptance, accepted: true, Now);

        (edited.Error.Code, edited.Error.Type).ShouldBe((ControlPlaneErrorCodes.TermsVersionLocked, ErrorType.Conflict));
        version.Values.ShouldBe(new TermsVersionValues(FirstOfDecember, Link, null, true));
    }

    [Fact]
    public void EditTermsVersion_AVersionWhoseDayCame_IsNotScheduled() =>
        Scheduled().Edit(FirstOfDecember, Link, null, true, false, new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero)).Error.Code
            .ShouldBe(ControlPlaneErrorCodes.TermsVersionNotScheduled);

    [Fact]
    public void EditTermsVersion_ADayInThePast_IsInvalid() =>
        Scheduled().Edit(Today.AddDays(-1), Link, null, true, false, Now).Error.Code.ShouldBe(ControlPlaneErrorCodes.TermsVersionEffectiveDatePast);

    [Fact]
    public void CancelTermsVersion_AScheduledVersion_KeepsWhoAndWhen()
    {
        var version = Scheduled();

        var cancelled = version.Cancel(Admin, Now);

        (cancelled.Value, version.CancelledAt, version.CancelledBy, version.IsScheduledOn(Today)).ShouldBe((StateChange.Changed, Now, Admin, false));
    }

    [Fact]
    public void CancelTermsVersion_ACancelledVersion_IsUnchanged()
    {
        var version = Scheduled();
        version.Cancel(Admin, Now);

        version.Cancel(Guid.CreateVersion7(), Now.AddMinutes(1)).Value.ShouldBe(StateChange.Unchanged);
    }

    [Fact]
    public void CancelTermsVersion_AVersionInForce_IsNotScheduled() =>
        TermsVersion.Create(Guid.CreateVersion7(), Today, Link, null, true, Admin, false, Now).Value.Cancel(Admin, Now).Error.Code
            .ShouldBe(ControlPlaneErrorCodes.TermsVersionNotScheduled);

    // 22:00 UTC is still the 14th in UTC, so the day of the version is the UTC day, whatever the server's zone.
    [Fact]
    public void DayOfTermsVersion_AMomentWithAnOffset_IsItsDayInUtc() =>
        TermsVersion.DayOf(new DateTimeOffset(2026, 11, 15, 1, 0, 0, TimeSpan.FromHours(3))).ShouldBe(Today);

    private static TermsVersion Scheduled() => TermsVersion.Create(Guid.CreateVersion7(), FirstOfDecember, Link, null, true, Admin, false, Now).Value;
}
