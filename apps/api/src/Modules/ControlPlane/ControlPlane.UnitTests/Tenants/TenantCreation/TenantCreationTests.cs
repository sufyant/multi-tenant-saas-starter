using ControlPlane.Application.Invitations.CancelInvitation;
using ControlPlane.Application.Invitations.OpenOwnerSignUp;
using ControlPlane.Application.Tenants.ActivateTenant;
using ControlPlane.Application.Tenants.CancelTenant;
using ControlPlane.Application.Tenants.RaiseTenantCreationAlarm;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Contracts.Invitations;
using Notifications.Contracts.Invitations;
using SharedKernel;
using Wolverine;

namespace ControlPlane.UnitTests.Tenants.TenantCreation;

// The tenant creation saga without a host (S12): it only decides, from its state and the message, what its next state is and which
// messages it sends. A message that does not fit the state is ignored (S7). One test for each row of the saga's state table.
public class TenantCreationTests
{
    private static readonly Guid TenantId = new("0199a8f0-0000-7000-8000-000000000201");
    private static readonly Guid InvitationId = new("0199a8f0-0000-7000-8000-000000000401");
    private static readonly Guid EventId = new("0199a8f0-0000-7000-8000-000000000501");
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
    private static readonly Uri AcceptLink = new("https://app.test/invitations/accept?code=0199a8f0-0000-7000-8000-000000000201.secret");
    private static readonly TenantCreationTimeouts Timeouts = new(TimeSpan.FromMinutes(10), TimeSpan.FromHours(2), TimeSpan.FromMinutes(15));
    private const string OwnerEmail = "owner@acme.test";

    // (start) | CreateTenantCommand handled | Activating | ActivateTenantCommand, ActivationTimedOut
    [Fact]
    public void CreateTenant_NewTenant_ActivatesTheTenantAndSchedulesTheActivationTimeout()
    {
        var (saga, activate, timeout) = TenantCreationSaga.Begin(TenantId, InvitationId, AcceptLink, Timeouts);

        saga.Id.ShouldBe(TenantId);
        saga.State.ShouldBe(TenantCreationSagaState.Activating);
        activate.ShouldBe(new ActivateTenantCommand(TenantId, InvitationId, AcceptLink));
        timeout.ShouldBe(new ActivationTimedOut(TenantId, TimeSpan.FromMinutes(10)));
    }

    // Activating | ActivateTenantReply | SendingInvitation | InvitationEmailTimedOut
    [Fact]
    public void ActivateTenant_TenantActivated_SchedulesTheInvitationEmailTimeout()
    {
        var saga = Activating();

        var sent = saga.Handle(Activated());

        saga.State.ShouldBe(TenantCreationSagaState.SendingInvitation);
        sent.ShouldBe([new InvitationEmailTimedOut(TenantId, TimeSpan.FromHours(2))]);
    }

    // Activating | Fault<ActivateTenantCommand> | Cancelling | CancelTenantCommand, CancellationTimedOut
    [Fact]
    public void ActivateTenant_FailsForGood_CancelsTheTenantAndSchedulesTheCancellationTimeout()
    {
        var saga = Activating();

        var sent = saga.Handle(FaultOf(new ActivateTenantCommand(TenantId, InvitationId, AcceptLink)));

        saga.State.ShouldBe(TenantCreationSagaState.Cancelling);
        sent.ShouldBe([new CancelTenantCommand(TenantId, InvitationId, "activation_failed"), new CancellationTimedOut(TenantId, TimeSpan.FromMinutes(15))]);
    }

    // Activating | ActivationTimedOut | NeedsAttention | CancelInvitationCommand, RaiseTenantCreationAlarmCommand
    [Fact]
    public void ActivateTenant_TimesOut_NeedsAttentionAndCancelsTheInvitation()
    {
        var saga = Activating();

        var sent = saga.Handle(new ActivationTimedOut(TenantId, Timeouts.Activation));

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBe([new CancelInvitationCommand(TenantId, InvitationId), new RaiseTenantCreationAlarmCommand(TenantId)]);
    }

    // Activating | SendInvitationEmailReply | Completed | none. The tenant's activation and the invitation email are published together,
    // so the email may be reported sent before the saga has heard of the activation; the email proves that the activation committed.
    [Fact]
    public void SendInvitation_EmailSentBeforeTheActivationArrives_CompletesTheTenantCreation()
    {
        var saga = Activating();

        var sent = saga.Handle(EmailSent());

        saga.State.ShouldBe(TenantCreationSagaState.Completed);
        sent.ShouldBeEmpty();
    }

    // SendingInvitation | SendInvitationEmailReply | Completed | none
    [Fact]
    public void SendInvitation_EmailSent_CompletesTheTenantCreation()
    {
        var saga = SendingInvitation();

        var sent = saga.Handle(EmailSent());

        saga.State.ShouldBe(TenantCreationSagaState.Completed);
        sent.ShouldBeEmpty();
    }

    // Activating | Fault<OwnerInvitationReadyEvent> | NeedsAttention | CancelInvitationCommand, RaiseTenantCreationAlarmCommand
    [Fact]
    public void SendInvitation_FailsForGoodBeforeTheActivationArrives_NeedsAttentionAndCancelsTheInvitation()
    {
        var saga = Activating();

        var sent = saga.Handle(FaultOf(InvitationReady()));

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBe([new CancelInvitationCommand(TenantId, InvitationId), new RaiseTenantCreationAlarmCommand(TenantId)]);
    }

    // SendingInvitation | Fault<OwnerInvitationReadyEvent> | NeedsAttention | CancelInvitationCommand, RaiseTenantCreationAlarmCommand
    [Fact]
    public void SendInvitation_FailsForGood_NeedsAttentionAndCancelsTheInvitation()
    {
        var saga = SendingInvitation();

        var sent = saga.Handle(FaultOf(InvitationReady()));

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBe([new CancelInvitationCommand(TenantId, InvitationId), new RaiseTenantCreationAlarmCommand(TenantId)]);
    }

    // SendingInvitation | Fault<OpenOwnerSignUpCommand> | NeedsAttention | CancelInvitationCommand, RaiseTenantCreationAlarmCommand
    [Fact]
    public void OpenOwnerSignUp_FailsForGood_NeedsAttentionAndCancelsTheInvitation()
    {
        var saga = SendingInvitation();

        var sent = saga.Handle(FaultOf(OwnerSignUp()));

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBe([new CancelInvitationCommand(TenantId, InvitationId), new RaiseTenantCreationAlarmCommand(TenantId)]);
    }

    // Activating | Fault<OpenOwnerSignUpCommand> | NeedsAttention | CancelInvitationCommand, RaiseTenantCreationAlarmCommand: the activation committed, as it
    // sent the sign-up, but its reply has not reached the saga yet.
    [Fact]
    public void OpenOwnerSignUp_FailsForGoodBeforeTheActivationArrives_NeedsAttentionAndCancelsTheInvitation()
    {
        var saga = Activating();

        var sent = saga.Handle(FaultOf(OwnerSignUp()));

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBe([new CancelInvitationCommand(TenantId, InvitationId), new RaiseTenantCreationAlarmCommand(TenantId)]);
    }

    // Completed | Fault<OpenOwnerSignUpCommand> | unchanged | none
    [Fact]
    public void OpenOwnerSignUp_FaultAfterCompletion_IsIgnored()
    {
        var saga = Completed();

        var sent = saga.Handle(FaultOf(OwnerSignUp()));

        saga.State.ShouldBe(TenantCreationSagaState.Completed);
        sent.ShouldBeEmpty();
    }

    // SendingInvitation | InvitationEmailTimedOut | NeedsAttention | CancelInvitationCommand, RaiseTenantCreationAlarmCommand
    [Fact]
    public void SendInvitation_TimesOut_NeedsAttentionAndCancelsTheInvitation()
    {
        var saga = SendingInvitation();

        var sent = saga.Handle(new InvitationEmailTimedOut(TenantId, Timeouts.InvitationEmail));

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBe([new CancelInvitationCommand(TenantId, InvitationId), new RaiseTenantCreationAlarmCommand(TenantId)]);
    }

    // Cancelling | CancelTenantReply | Cancelled | none
    [Fact]
    public void CancelTenant_TenantCancelled_EndsCancelled()
    {
        var saga = Cancelling();

        var sent = saga.Handle(new CancelTenantReply(TenantId));

        saga.State.ShouldBe(TenantCreationSagaState.Cancelled);
        sent.ShouldBeEmpty();
    }

    // Cancelling | CancellationTimedOut | NeedsAttention | RaiseTenantCreationAlarmCommand
    [Fact]
    public void CancelTenant_TimesOut_NeedsAttention()
    {
        var saga = Cancelling();

        var sent = saga.Handle(new CancellationTimedOut(TenantId, Timeouts.Cancellation));

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBe([new RaiseTenantCreationAlarmCommand(TenantId)]);
    }

    // Cancelling | Fault<CancelTenantCommand> | NeedsAttention | RaiseTenantCreationAlarmCommand. S9: a compensation that fails for good leaves the
    // process to a person.
    [Fact]
    public void CancelTenant_FailsForGood_NeedsAttention()
    {
        var saga = Cancelling();

        var sent = saga.Handle(FaultOf(new CancelTenantCommand(TenantId, InvitationId, "activation_failed")));

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBe([new RaiseTenantCreationAlarmCommand(TenantId)]);
    }

    // Any state except NeedsAttention | Fault<CancelInvitationCommand> | NeedsAttention | RaiseTenantCreationAlarmCommand
    [Fact]
    public void CancelInvitation_FailsForGoodWhileCancelling_NeedsAttention()
    {
        var saga = Cancelling();

        var sent = saga.Handle(FaultOf(new CancelInvitationCommand(TenantId, InvitationId)));

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBe([new RaiseTenantCreationAlarmCommand(TenantId)]);
    }

    // NeedsAttention | any message | unchanged | none. M11: the alarm is raised once for each tenant creation.
    [Fact]
    public void CancelInvitation_FailsForGoodAfterTheAlarm_RaisesNoSecondAlarm()
    {
        var saga = SendingInvitation();
        saga.Handle(FaultOf(InvitationReady()));

        var sent = saga.Handle(FaultOf(new CancelInvitationCommand(TenantId, InvitationId)));

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBeEmpty();
    }

    // Completed | any message | unchanged | none
    [Fact]
    public void SendInvitation_TimeoutAfterCompletion_IsIgnored()
    {
        var saga = Completed();

        var sent = saga.Handle(new InvitationEmailTimedOut(TenantId, Timeouts.InvitationEmail));

        saga.State.ShouldBe(TenantCreationSagaState.Completed);
        sent.ShouldBeEmpty();
    }

    // Cancelled | any message | unchanged | none
    [Fact]
    public void CancelTenant_TimeoutAfterTheTenantIsCancelled_IsIgnored()
    {
        var saga = Cancelling();
        saga.Handle(new CancelTenantReply(TenantId));

        var sent = saga.Handle(new CancellationTimedOut(TenantId, Timeouts.Cancellation));

        saga.State.ShouldBe(TenantCreationSagaState.Cancelled);
        sent.ShouldBeEmpty();
    }

    // M2: the invitation was cancelled when the wait timed out, so the late fault has nothing left to do.
    [Fact]
    public void SendInvitation_FaultAfterTheTimeout_IsIgnored()
    {
        var saga = SendingInvitation();
        saga.Handle(new InvitationEmailTimedOut(TenantId, Timeouts.InvitationEmail));

        var sent = saga.Handle(FaultOf(InvitationReady()));

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBeEmpty();
    }

    // A late email carries a link to the cancelled invitation, which answers 404; the alarm already asked for a new invitation.
    [Fact]
    public void SendInvitation_EmailSentAfterTheTimeout_IsIgnored()
    {
        var saga = SendingInvitation();
        saga.Handle(new InvitationEmailTimedOut(TenantId, Timeouts.InvitationEmail));

        var sent = saga.Handle(EmailSent());

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBeEmpty();
    }

    // M11: a repeated alarm reaches the saga as a repeated timeout.
    [Fact]
    public void ActivateTenant_TimeoutArrivesAgainAfterTheAlarm_RaisesNoSecondAlarm()
    {
        var saga = Activating();
        saga.Handle(new ActivationTimedOut(TenantId, Timeouts.Activation));

        var sent = saga.Handle(new ActivationTimedOut(TenantId, Timeouts.Activation));

        saga.State.ShouldBe(TenantCreationSagaState.NeedsAttention);
        sent.ShouldBeEmpty();
    }

    [Fact]
    public void ActivateTenant_ActivationArrivesAfterTheEmailWasSent_IsIgnored()
    {
        var saga = Activating();
        saga.Handle(EmailSent());

        var sent = saga.Handle(Activated());

        saga.State.ShouldBe(TenantCreationSagaState.Completed);
        sent.ShouldBeEmpty();
    }

    [Fact]
    public void ActivateTenant_TimeoutAfterTheActivation_IsIgnored()
    {
        var saga = SendingInvitation();

        var sent = saga.Handle(new ActivationTimedOut(TenantId, Timeouts.Activation));

        saga.State.ShouldBe(TenantCreationSagaState.SendingInvitation);
        sent.ShouldBeEmpty();
    }

    [Fact]
    public void SendInvitation_EmailSentAgain_SendsNothing()
    {
        var saga = Completed();

        var sent = saga.Handle(EmailSent());

        saga.State.ShouldBe(TenantCreationSagaState.Completed);
        sent.ShouldBeEmpty();
    }

    private static TenantCreationSaga Activating() => TenantCreationSaga.Begin(TenantId, InvitationId, AcceptLink, Timeouts).Saga;

    private static TenantCreationSaga SendingInvitation()
    {
        var saga = Activating();
        saga.Handle(Activated());
        return saga;
    }

    private static TenantCreationSaga Completed()
    {
        var saga = SendingInvitation();
        saga.Handle(EmailSent());
        return saga;
    }

    private static TenantCreationSaga Cancelling()
    {
        var saga = Activating();
        saga.Handle(FaultOf(new ActivateTenantCommand(TenantId, InvitationId, AcceptLink)));
        return saga;
    }

    private static ActivateTenantReply Activated() => new(TenantId);

    private static OwnerInvitationReadyEvent InvitationReady() =>
        new(EventId, Now, TenantId, InvitationId, OwnerEmail, "Acme Ltd", AcceptLink, Language.En, TimeSpan.FromDays(7));

    private static OpenOwnerSignUpCommand OwnerSignUp() => new(TenantId, InvitationId, OwnerEmail, "Acme Ltd", AcceptLink, Now.AddDays(7));

    private static SendInvitationEmailReply EmailSent() => new(EventId, Now, TenantId, InvitationId);

    // What Wolverine publishes once a message has gone to the dead letter queue; it carries the exception's type only.
    private static Fault<T> FaultOf<T>(T message)
        where T : class =>
        new(
            message,
            ExceptionInfo.From(new InvalidOperationException(), includeMessage: false, includeStackTrace: false),
            4,
            Now,
            null,
            Guid.Empty,
            TenantId.ToString(),
            null,
            new Dictionary<string, string?>());
}
