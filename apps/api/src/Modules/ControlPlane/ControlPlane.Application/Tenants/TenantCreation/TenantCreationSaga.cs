using ControlPlane.Application.Invitations.CancelInvitation;
using ControlPlane.Application.Invitations.OpenOwnerSignUp;
using ControlPlane.Application.Tenants.ActivateTenant;
using ControlPlane.Application.Tenants.CancelTenant;
using ControlPlane.Application.Tenants.RaiseTenantCreationAlarm;
using ControlPlane.Contracts.Invitations;
using Notifications.Contracts.Invitations;
using SharedKernel;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace ControlPlane.Application.Tenants.TenantCreation;

// The messages of the saga inside ControlPlane carry the new tenant, which is also the saga's id; the envelope carries the same tenant,
// so every step runs in it. The events that leave the module are in ControlPlane.Contracts and Notifications.Contracts. Each step and
// compensation (ActivateTenantHandler, CancelTenantHandler, CancelInvitationHandler) runs in the new tenant, may run twice (S7), and
// returns its result as a message. A step that fails for good goes to the dead letter queue, and the saga hears of it through the fault
// Wolverine publishes.

/// <summary>
/// The tenant creation process of section 6, orchestrated in one place (S3, S4). Its id is the tenant's id, and its record belongs
/// to the tenant. It only decides: from its state and a message it chooses its next state and the messages to send, and it calls
/// nothing (S12). A message that does not fit its state is ignored (S7). The record stays when the process ends, so a late or
/// repeated message still finds the state it is ignored by, and the record says where the process ended.
/// </summary>
/// <remarks>
/// Steps: start (compensatable), activate the tenant (pivot), let the owner sign up and send the invitation email (retryable) (S5). A
/// step that fails for good
/// reaches the saga as Wolverine's fault of its message. Every wait has a timeout (S10). A wait that times out while the invitation
/// may still be open cancels it, so a late email carries a link that answers 404. Completed, Cancelled and NeedsAttention are final:
/// the alarm is raised once for each tenant creation.
/// </remarks>
public sealed class TenantCreationSaga : Saga, ITenantEntity
{
    private const string ActivationFailed = "activation_failed";

    private TenantCreationSaga()
    {
    }

    public Guid Id { get; private set; }

    public TenantCreationSagaState State { get; private set; }

    public Guid InvitationId { get; private set; }

    /// <summary>Fixed when the tenant creation starts, so a configuration change does not move a deadline already set.</summary>
    public TimeSpan InvitationEmailTimeout { get; private set; }

    /// <summary>Fixed when the tenant creation starts, like <see cref="InvitationEmailTimeout"/>.</summary>
    public TimeSpan CancellationTimeout { get; private set; }

    private bool IsActivatingOrSendingInvitation => State is TenantCreationSagaState.Activating or TenantCreationSagaState.SendingInvitation;

    private bool IsFinal => State is TenantCreationSagaState.Completed or TenantCreationSagaState.Cancelled or TenantCreationSagaState.NeedsAttention;

    /// <summary>Step 1 starts the tenant creation: the tenant is activated with the link the owner is invited by, within the activation timeout.</summary>
    public static (TenantCreationSaga Saga, ActivateTenantCommand Activate, ActivationTimedOut Timeout) Begin(
        Guid tenantId,
        Guid invitationId,
        Uri acceptLink,
        TenantCreationTimeouts timeouts) =>
        (
            new TenantCreationSaga
            {
                Id = tenantId,
                State = TenantCreationSagaState.Activating,
                InvitationId = invitationId,
                InvitationEmailTimeout = timeouts.InvitationEmail,
                CancellationTimeout = timeouts.Cancellation,
            },
            new ActivateTenantCommand(tenantId, invitationId, acceptLink),
            new ActivationTimedOut(tenantId, timeouts.Activation));

    public OutgoingMessages Handle([SagaIdentityFrom(nameof(ActivateTenantReply.TenantId))] ActivateTenantReply activated) =>
        State == TenantCreationSagaState.Activating
            ? MoveTo(TenantCreationSagaState.SendingInvitation, new InvitationEmailTimedOut(Id, InvitationEmailTimeout))
            : [];

    public OutgoingMessages Handle(Fault<ActivateTenantCommand> fault) =>
        State == TenantCreationSagaState.Activating
            ? MoveTo(
                TenantCreationSagaState.Cancelling,
                new CancelTenantCommand(Id, InvitationId, ActivationFailed),
                new CancellationTimedOut(Id, CancellationTimeout))
            : [];

    // The activation may still commit later; the invitation is closed meanwhile, so the email it sends leads nowhere.
    public OutgoingMessages Handle([SagaIdentityFrom(nameof(ActivationTimedOut.TenantId))] ActivationTimedOut timeout) =>
        State == TenantCreationSagaState.Activating ? NeedsAttentionWithTheInvitationCancelled() : [];

    // The email can be reported before the activation reaches the saga: both are published by the activation, so the report proves
    // that the activation committed.
    public OutgoingMessages Handle([SagaIdentityFrom(nameof(SendInvitationEmailReply.TenantId))] SendInvitationEmailReply sent) =>
        IsActivatingOrSendingInvitation ? MoveTo(TenantCreationSagaState.Completed) : [];

    // After the pivot nothing is undone: the tenant stays active, and the invitation nobody received is cancelled. The identity provider
    // that did not let the owner sign up, and the email that did not go out, end the same way.
    public OutgoingMessages Handle(Fault<OpenOwnerSignUpCommand> fault) =>
        IsActivatingOrSendingInvitation ? NeedsAttentionWithTheInvitationCancelled() : [];

    public OutgoingMessages Handle(Fault<OwnerInvitationReadyEvent> fault) =>
        IsActivatingOrSendingInvitation ? NeedsAttentionWithTheInvitationCancelled() : [];

    public OutgoingMessages Handle([SagaIdentityFrom(nameof(InvitationEmailTimedOut.TenantId))] InvitationEmailTimedOut timeout) =>
        State == TenantCreationSagaState.SendingInvitation ? NeedsAttentionWithTheInvitationCancelled() : [];

    public OutgoingMessages Handle([SagaIdentityFrom(nameof(CancelTenantReply.TenantId))] CancelTenantReply cancelled) =>
        State == TenantCreationSagaState.Cancelling ? MoveTo(TenantCreationSagaState.Cancelled) : [];

    // An active tenant is never cancelled: the compensation reports nothing, and its wait times out.
    public OutgoingMessages Handle([SagaIdentityFrom(nameof(CancellationTimedOut.TenantId))] CancellationTimedOut timeout) =>
        State == TenantCreationSagaState.Cancelling ? NeedsAttention() : [];

    // A compensation that fails for good leaves the process to a person (S9).
    public OutgoingMessages Handle(Fault<CancelTenantCommand> fault) => State == TenantCreationSagaState.Cancelling ? NeedsAttention() : [];

    public OutgoingMessages Handle(Fault<CancelInvitationCommand> fault) => IsFinal ? [] : NeedsAttention();

    private OutgoingMessages NeedsAttentionWithTheInvitationCancelled() =>
        MoveTo(TenantCreationSagaState.NeedsAttention, new CancelInvitationCommand(Id, InvitationId), new RaiseTenantCreationAlarmCommand(Id));

    private OutgoingMessages NeedsAttention() => MoveTo(TenantCreationSagaState.NeedsAttention, new RaiseTenantCreationAlarmCommand(Id));

    private OutgoingMessages MoveTo(TenantCreationSagaState state, params object[] messages)
    {
        State = state;
        return [.. messages];
    }
}
