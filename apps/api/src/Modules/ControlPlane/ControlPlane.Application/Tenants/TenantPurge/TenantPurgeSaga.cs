using ControlPlane.Application.Tenants.CompleteTenantPurge;
using ControlPlane.Application.Tenants.RaiseTenantPurgeAlarm;
using ControlPlane.Contracts.Tenants;
using SharedKernel;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace ControlPlane.Application.Tenants.TenantPurge;

// The steps of the purge run in the tenant: every message of the saga carries the tenant, which is also the saga's id, and so does its
// envelope, so each step's transaction declares it (R4). The modules hear one PurgeTenantDataCommand, which Wolverine hands to each
// module's handler on a queue of its own (W4), and each replies with its name (S13).

/// <summary>
/// The tenant purge of section 6, the template's reference use of a saga (S1 to S13). It is a forward recovery saga: its pivot, the
/// tenant's move to <c>Purging</c>, comes first, and every step after it only retries; nothing is undone (S5; Richardson, Microservices
/// Patterns, chapter 4). Its id is the tenant's id, and its record belongs to the tenant. It only decides: from its state and a message
/// it chooses its next state and the messages to send, and it calls nothing (S12). A message that does not fit its state is ignored
/// (S7). The record stays when the purge ends, so a late or repeated message still finds the state it is ignored by, and the record says
/// where the purge ended.
/// </summary>
/// <remarks>
/// Steps: ask every participating module to purge the tenant's data and wait for each reply; then ControlPlane purges its own data and
/// leaves the tombstone. The wait has a timeout (S10): it raises an alarm that names the modules still waiting, and the purge keeps
/// waiting, so a late reply still completes it.
/// </remarks>
public sealed class TenantPurgeSaga : Saga, ITenantEntity
{
    /// <summary>The name of ControlPlane's own last step, which the purge waits for after the modules.</summary>
    public const string ControlPlane = "ControlPlane";

    private TenantPurgeSaga()
    {
    }

    public Guid Id { get; private set; }

    public TenantPurgeSagaState State { get; private set; }

    /// <summary>The steps the purge still waits for: the modules that have not replied, then ControlPlane's own.</summary>
    public List<string> Waiting { get; private set; } = [];

    /// <summary>
    /// The purge starts, after its pivot: every participating module is asked to purge the tenant's data, within the timeout. Without a
    /// participant, ControlPlane's own step follows at once.
    /// </summary>
    public static (TenantPurgeSaga Saga, OutgoingMessages Messages) Begin(Guid tenantId, IEnumerable<string> participants, TimeSpan timeout)
    {
        var saga = new TenantPurgeSaga { Id = tenantId, State = TenantPurgeSagaState.PurgingModules, Waiting = [.. participants.Distinct().Order(StringComparer.Ordinal)] };
        object first = saga.Waiting.Count > 0 ? new PurgeTenantDataCommand(tenantId) : saga.Finish();
        return (saga, [first, new PurgeTimedOut(tenantId, timeout)]);
    }

    public OutgoingMessages Handle([SagaIdentityFrom(nameof(PurgeTenantDataReply.TenantId))] PurgeTenantDataReply purged)
    {
        if (State != TenantPurgeSagaState.PurgingModules || !Waiting.Remove(purged.Module))
        {
            return [];
        }

        return Waiting.Count == 0 ? [Finish()] : [];
    }

    public OutgoingMessages Handle([SagaIdentityFrom(nameof(CompleteTenantPurgeReply.TenantId))] CompleteTenantPurgeReply completed)
    {
        if (State == TenantPurgeSagaState.Finishing)
        {
            State = TenantPurgeSagaState.Completed;
            Waiting = [];
        }

        return [];
    }

    // The alarm comes once, when the timeout comes; the purge keeps waiting after it.
    public OutgoingMessages Handle([SagaIdentityFrom(nameof(PurgeTimedOut.TenantId))] PurgeTimedOut timeout) =>
        State == TenantPurgeSagaState.Completed ? [] : [new RaiseTenantPurgeAlarmCommand(Id, [.. Waiting])];

    private CompleteTenantPurgeCommand Finish()
    {
        State = TenantPurgeSagaState.Finishing;
        Waiting = [ControlPlane];
        return new CompleteTenantPurgeCommand(Id);
    }
}
