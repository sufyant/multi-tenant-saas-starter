using ControlPlane.Contracts;
using Notifications.Contracts;
using SharedKernel;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace ControlPlane.Application.Tenants.TenantCreation;

public enum TenantCreationSagaState
{
    Activating,
    SendingInvitation,
    Completed,
    Cancelling,
    Cancelled,
    NeedsAttention,
}

/// <summary>How long the tenant creation waits for the activation, the invitation email and the cancellation (S10).</summary>
public sealed record TenantCreationTimeouts(TimeSpan Activation, TimeSpan InvitationEmail, TimeSpan Cancellation);

/// <summary>
/// The tenant creation process of section 6, orchestrated in one place (S3, S4). Its id is the tenant's id, and its record belongs
/// to the tenant. It only decides: from its state and a message it chooses its next state and the messages to send, and it calls
/// nothing (S12). A message that does not fit its state is ignored (S7). The record stays when the process ends, so a late or
/// repeated message still finds the state it is ignored by, and the record says where the process ended.
/// </summary>
/// <remarks>
/// Steps: start (compensatable), activate the tenant (pivot), send the invitation email (retryable) (S5). A step that fails for good
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

    /// <summary>Step 1 starts the tenant creation: the tenant is activated with the link the owner is invited by, within the activation timeout.</summary>
    public static (TenantCreationSaga Saga, ActivateTenant Activate, ActivationTimedOut Timeout) Begin(
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
            new ActivateTenant(tenantId, invitationId, acceptLink),
            new ActivationTimedOut(tenantId, timeouts.Activation));

    public OutgoingMessages Handle([SagaIdentityFrom(nameof(ActivateTenantReply.TenantId))] ActivateTenantReply activated) =>
        State == TenantCreationSagaState.Activating
            ? MoveTo(TenantCreationSagaState.SendingInvitation, new InvitationEmailTimedOut(Id, InvitationEmailTimeout))
            : [];

    public OutgoingMessages Handle(Fault<ActivateTenant> fault) =>
        State == TenantCreationSagaState.Activating
            ? MoveTo(
                TenantCreationSagaState.Cancelling,
                new CancelTenant(Id, InvitationId, ActivationFailed),
                new CancellationTimedOut(Id, CancellationTimeout))
            : [];

    // The activation may still commit later; the invitation is closed meanwhile, so the email it sends leads nowhere.
    public OutgoingMessages Handle([SagaIdentityFrom(nameof(ActivationTimedOut.TenantId))] ActivationTimedOut timeout) =>
        State == TenantCreationSagaState.Activating ? NeedsAttentionWithTheInvitationCancelled() : [];

    // The email can be reported before the activation reaches the saga: both are published by the activation, so the report proves
    // that the activation committed.
    public OutgoingMessages Handle([SagaIdentityFrom(nameof(SendInvitationEmailReply.TenantId))] SendInvitationEmailReply sent) =>
        IsSendingInvitation ? MoveTo(TenantCreationSagaState.Completed) : [];

    // After the pivot nothing is undone: the tenant stays active, and the invitation nobody received is withdrawn.
    public OutgoingMessages Handle(Fault<OwnerInvitationReady> fault) =>
        IsSendingInvitation ? NeedsAttentionWithTheInvitationCancelled() : [];

    public OutgoingMessages Handle([SagaIdentityFrom(nameof(InvitationEmailTimedOut.TenantId))] InvitationEmailTimedOut timeout) =>
        State == TenantCreationSagaState.SendingInvitation ? NeedsAttentionWithTheInvitationCancelled() : [];

    public OutgoingMessages Handle([SagaIdentityFrom(nameof(CancelTenantReply.TenantId))] CancelTenantReply cancelled) =>
        State == TenantCreationSagaState.Cancelling ? MoveTo(TenantCreationSagaState.Cancelled) : [];

    // An active tenant is never cancelled: the compensation reports nothing, and its wait times out.
    public OutgoingMessages Handle([SagaIdentityFrom(nameof(CancellationTimedOut.TenantId))] CancellationTimedOut timeout) =>
        State == TenantCreationSagaState.Cancelling ? NeedsAttention() : [];

    // A compensation that fails for good leaves the process to a person (S9).
    public OutgoingMessages Handle(Fault<CancelTenant> fault) => State == TenantCreationSagaState.Cancelling ? NeedsAttention() : [];

    public OutgoingMessages Handle(Fault<CancelInvitation> fault) => IsFinal ? [] : NeedsAttention();

    private bool IsSendingInvitation => State is TenantCreationSagaState.Activating or TenantCreationSagaState.SendingInvitation;

    private bool IsFinal => State is TenantCreationSagaState.Completed or TenantCreationSagaState.Cancelled or TenantCreationSagaState.NeedsAttention;

    private OutgoingMessages NeedsAttentionWithTheInvitationCancelled() =>
        MoveTo(TenantCreationSagaState.NeedsAttention, new CancelInvitation(Id, InvitationId), new RaiseTenantCreationAlarm(Id));

    private OutgoingMessages NeedsAttention() => MoveTo(TenantCreationSagaState.NeedsAttention, new RaiseTenantCreationAlarm(Id));

    private OutgoingMessages MoveTo(TenantCreationSagaState state, params object[] messages)
    {
        State = state;
        return [.. messages];
    }
}
