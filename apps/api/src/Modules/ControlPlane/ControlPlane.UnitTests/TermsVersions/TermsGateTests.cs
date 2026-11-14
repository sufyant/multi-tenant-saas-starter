using ControlPlane.Domain.TermsVersions;

namespace ControlPlane.UnitTests.TermsVersions;

public sealed class TermsGateTests
{
    private static readonly DateTimeOffset UserCreated = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Later = UserCreated.AddMonths(6);

    private static readonly HashSet<Guid> None = [];

    private static readonly TermsGateVersion CurrentToAccept = new(Guid.CreateVersion7(), true, true, Later);

    private static readonly TermsGateVersion ScheduledToAccept = new(Guid.CreateVersion7(), true, true, Later);

    private static readonly TermsGateVersion CurrentNotice = new(Guid.CreateVersion7(), false, true, Later);

    private static readonly TermsGateVersion ScheduledNotice = new(Guid.CreateVersion7(), false, true, Later);

    [Fact]
    public void AskTerms_NoVersion_AsksNothing() => TermsGate.For(null, null, None, None, UserCreated).ShouldBe(TermsAsk.Nothing);

    [Fact]
    public void AskTerms_TwoVersionsToAccept_AsksForTheVersionInForceFirst() =>
        TermsGate.For(CurrentToAccept, ScheduledToAccept, None, None, UserCreated).ShouldBe(new TermsAsk(TermsAction.Accept, CurrentToAccept.Id));

    [Fact]
    public void AskTerms_TheVersionInForceAccepted_AsksForTheScheduledOne() =>
        TermsGate.For(CurrentToAccept, ScheduledToAccept, new HashSet<Guid> { CurrentToAccept.Id }, None, UserCreated)
            .ShouldBe(new TermsAsk(TermsAction.Accept, ScheduledToAccept.Id));

    [Fact]
    public void AskTerms_AnAcceptanceAndANotice_AsksForTheAcceptance() =>
        TermsGate.For(CurrentNotice, ScheduledToAccept, None, None, UserCreated).ShouldBe(new TermsAsk(TermsAction.Accept, ScheduledToAccept.Id));

    [Fact]
    public void AskTerms_TwoNotices_GivesTheScheduledOneFirst() =>
        TermsGate.For(CurrentNotice, ScheduledNotice, None, None, UserCreated).ShouldBe(new TermsAsk(TermsAction.Notice, ScheduledNotice.Id));

    [Fact]
    public void AskTerms_TheScheduledNoticeDismissed_GivesTheNoticeInForce() =>
        TermsGate.For(CurrentNotice, ScheduledNotice, None, new HashSet<Guid> { ScheduledNotice.Id }, UserCreated)
            .ShouldBe(new TermsAsk(TermsAction.Notice, CurrentNotice.Id));

    [Fact]
    public void AskTerms_ANoticeOfAVersionTheUserAccepted_AsksNothing() =>
        TermsGate.For(CurrentNotice, null, new HashSet<Guid> { CurrentNotice.Id }, None, UserCreated).ShouldBe(TermsAsk.Nothing);

    [Fact]
    public void AskTerms_ANoticeOfAVersionCreatedBeforeTheUser_AsksNothing() =>
        TermsGate.For(CurrentNotice, null, None, None, Later.AddDays(1)).ShouldBe(TermsAsk.Nothing);

    // A small change emails nobody, and gives no notice either.
    [Fact]
    public void AskTerms_AVersionWithoutEmail_GivesNoNotice() =>
        TermsGate.For(new TermsGateVersion(Guid.CreateVersion7(), false, false, Later), null, None, None, UserCreated).ShouldBe(TermsAsk.Nothing);

    [Fact]
    public void AskTerms_AVersionToAcceptWithoutEmail_StillAsksForIt()
    {
        var quiet = new TermsGateVersion(Guid.CreateVersion7(), true, false, Later);

        TermsGate.For(quiet, null, None, None, UserCreated).ShouldBe(new TermsAsk(TermsAction.Accept, quiet.Id));
    }

    [Fact]
    public void AskTerms_AVersionToAcceptCreatedBeforeTheUser_StillAsksForIt() =>
        TermsGate.For(CurrentToAccept, null, None, None, Later.AddDays(1)).ShouldBe(new TermsAsk(TermsAction.Accept, CurrentToAccept.Id));
}
