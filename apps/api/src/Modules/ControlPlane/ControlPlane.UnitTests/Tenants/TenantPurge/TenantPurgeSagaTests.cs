using ControlPlane.Application.Tenants.CompleteTenantPurge;
using ControlPlane.Application.Tenants.RaiseTenantPurgeAlarm;
using ControlPlane.Application.Tenants.TenantPurge;
using ControlPlane.Contracts.Tenants;

namespace ControlPlane.UnitTests.Tenants.TenantPurge;

// The tenant purge of section 6, without a host (S12): from its state and a message the saga chooses its next state and the messages to
// send. It asks every participating module to purge the tenant's data, waits for each reply, raises an alarm that names the modules
// still waiting when its timeout comes, and finishes the tenant last. A message that does not fit its state is ignored (S7).
public sealed class TenantPurgeSagaTests
{
    private const string Audit = "Audit";

    private const string Notifications = "Notifications";

    private static readonly Guid TenantId = new("0199a8f0-0000-7000-8000-000000000701");

    private static readonly TimeSpan Timeout = TimeSpan.FromHours(1);

    [Fact]
    public void StartTenantPurge_TwoParticipants_AsksBothAndWaitsForEachWithItsTimeout()
    {
        var (saga, messages) = TenantPurgeSaga.Begin(TenantId, [Notifications, Audit], Timeout);

        (saga.Id, saga.State).ShouldBe((TenantId, TenantPurgeSagaState.PurgingModules));
        saga.Waiting.ShouldBe([Audit, Notifications]);
        messages.ShouldBe([new PurgeTenantDataCommand(TenantId), new PurgeTimedOut(TenantId, Timeout)]);
    }

    // The happy path: when the last module has replied, ControlPlane purges its own data and finishes the tenant last.
    [Fact]
    public void PurgeTenantData_EveryModuleReplied_FinishesTheTenantInControlPlane()
    {
        var saga = Begun();

        var first = saga.Handle(new PurgeTenantDataReply(TenantId, Audit));
        var last = saga.Handle(new PurgeTenantDataReply(TenantId, Notifications));

        first.ShouldBeEmpty();
        last.ShouldBe([new CompleteTenantPurgeCommand(TenantId)]);
        saga.State.ShouldBe(TenantPurgeSagaState.Finishing);
        saga.Waiting.ShouldBe([TenantPurgeSaga.ControlPlane]);
    }

    [Fact]
    public void CompleteTenantPurge_ControlPlaneReplied_IsCompletedAndWaitsForNothing()
    {
        var saga = Finishing();

        var messages = saga.Handle(new CompleteTenantPurgeReply(TenantId));

        messages.ShouldBeEmpty();
        saga.State.ShouldBe(TenantPurgeSagaState.Completed);
        saga.Waiting.ShouldBeEmpty();
    }

    // Any message may arrive twice (O4): a second reply of a module changes nothing and sends nothing, so ControlPlane's step is asked
    // once.
    [Fact]
    public void PurgeTenantData_AReplyThatArrivesTwice_ChangesNothingTheSecondTime()
    {
        var saga = Begun();
        saga.Handle(new PurgeTenantDataReply(TenantId, Audit));

        var again = saga.Handle(new PurgeTenantDataReply(TenantId, Audit));

        again.ShouldBeEmpty();
        saga.State.ShouldBe(TenantPurgeSagaState.PurgingModules);
        saga.Waiting.ShouldBe([Notifications]);
    }

    [Fact]
    public void PurgeTenantData_TheLastReplyArrivesTwice_FinishesTheTenantOnce()
    {
        var saga = Finishing();

        var again = saga.Handle(new PurgeTenantDataReply(TenantId, Notifications));

        again.ShouldBeEmpty();
        saga.State.ShouldBe(TenantPurgeSagaState.Finishing);
    }

    // A reply names the module that sent it; a name the purge does not wait for changes nothing.
    [Fact]
    public void PurgeTenantData_AReplyOfAModuleItDoesNotWaitFor_ChangesNothing()
    {
        var saga = Begun();

        var messages = saga.Handle(new PurgeTenantDataReply(TenantId, "Billing"));

        messages.ShouldBeEmpty();
        saga.Waiting.ShouldBe([Audit, Notifications]);
    }

    // S10: the wait has a timeout. The alarm names every module that has not replied yet, and the purge keeps waiting for them.
    [Fact]
    public void TimeOutThePurge_ModulesStillWaiting_RaisesTheAlarmThatNamesThemAndKeepsWaiting()
    {
        var saga = Begun();
        saga.Handle(new PurgeTenantDataReply(TenantId, Audit));

        var messages = saga.Handle(new PurgeTimedOut(TenantId, Timeout));

        messages.ShouldHaveSingleItem().ShouldBeOfType<RaiseTenantPurgeAlarmCommand>().ShouldSatisfyAllConditions(
            alarm => alarm.TenantId.ShouldBe(TenantId),
            alarm => alarm.Waiting.ShouldBe([Notifications]));
        saga.State.ShouldBe(TenantPurgeSagaState.PurgingModules);
        saga.Waiting.ShouldBe([Notifications]);
    }

    // ControlPlane's own step is waited for too, so a step that never ends raises the alarm with ControlPlane's name.
    [Fact]
    public void TimeOutThePurge_ControlPlaneStillFinishing_RaisesTheAlarmThatNamesControlPlane()
    {
        var saga = Finishing();

        var messages = saga.Handle(new PurgeTimedOut(TenantId, Timeout));

        messages.ShouldHaveSingleItem().ShouldBeOfType<RaiseTenantPurgeAlarmCommand>().Waiting.ShouldBe([TenantPurgeSaga.ControlPlane]);
    }

    // A late reply after the alarm still completes the purge.
    [Fact]
    public void PurgeTenantData_ALateReplyAfterTheAlarm_StillFinishesTheTenant()
    {
        var saga = Begun();
        saga.Handle(new PurgeTenantDataReply(TenantId, Audit));
        saga.Handle(new PurgeTimedOut(TenantId, Timeout));

        var late = saga.Handle(new PurgeTenantDataReply(TenantId, Notifications));

        late.ShouldBe([new CompleteTenantPurgeCommand(TenantId)]);
        saga.State.ShouldBe(TenantPurgeSagaState.Finishing);
    }

    [Fact]
    public void TimeOutThePurge_Completed_RaisesNoAlarm()
    {
        var saga = Finishing();
        saga.Handle(new CompleteTenantPurgeReply(TenantId));

        var messages = saga.Handle(new PurgeTimedOut(TenantId, Timeout));

        messages.ShouldBeEmpty();
    }

    // A template without participants still finishes the tenant.
    [Fact]
    public void StartTenantPurge_NoParticipant_FinishesTheTenantAtOnce()
    {
        var (saga, messages) = TenantPurgeSaga.Begin(TenantId, [], Timeout);

        messages.ShouldBe([new CompleteTenantPurgeCommand(TenantId), new PurgeTimedOut(TenantId, Timeout)]);
        saga.State.ShouldBe(TenantPurgeSagaState.Finishing);
        saga.Waiting.ShouldBe([TenantPurgeSaga.ControlPlane]);
    }

    private static TenantPurgeSaga Begun() => TenantPurgeSaga.Begin(TenantId, [Audit, Notifications], Timeout).Saga;

    private static TenantPurgeSaga Finishing()
    {
        var saga = Begun();
        saga.Handle(new PurgeTenantDataReply(TenantId, Audit));
        saga.Handle(new PurgeTenantDataReply(TenantId, Notifications));
        return saga;
    }
}
