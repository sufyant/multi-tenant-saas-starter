using System.Collections.Frozen;
using ControlPlane.Domain;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.UnitTests.Tenants;

// The rules of the Tenant aggregate (M1): its creation, status and name, its Owner invitation, and the Owner rules of a
// role change and a removal (K1). Each check is pure up to the change, so a rejection changes nothing.
public sealed class TenantTests
{
    private const string Reason = "Unpaid invoices";

    private const int DailyLimit = 50;

    private const int RoleLimit = 20;

    private const string MembersRead = PermissionPools.TenantMembersRead;

    private const string MembersUpdate = PermissionPools.TenantMembersUpdate;

    private const string MembersDelete = PermissionPools.TenantMembersDelete;

    private const string ProfileRead = PermissionPools.TenantProfileRead;

    private const string InvitationsCreate = PermissionPools.TenantInvitationsCreate;

    private const string InvitationsRead = PermissionPools.TenantInvitationsRead;

    private const string InvitationsDelete = PermissionPools.TenantInvitationsDelete;

    private const string RolesRead = PermissionPools.TenantRolesRead;

    private const string AuditRead = PermissionPools.TenantAuditRead;

    private static readonly Guid Id = new("0199a8f0-0000-7000-8000-000000000001");

    // The tenant's roles, which say what each membership's role may do (A1). Each tenant has its own (A2).
    private static readonly TenantRoles Roles = new([.. Enum.GetValues<BuiltInRole>().Select(builtIn => Role.CreateBuiltIn(Guid.NewGuid(), Id, builtIn))]);

    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan ReopenPeriod = TimeSpan.FromDays(30);

    private static readonly Guid InviterId = new("0199a8f0-0000-7000-8000-000000000002");

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private static readonly Inviter Platform = Inviter.Platform(InviterId, Language.Tr);

    private static readonly AcceptanceFacts NewPerson = new(UserDeleted: false, IsSystemAdmin: false, IsMember: false);

    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly Guid PlanId = new("0199a8f0-0000-7000-8000-0000000000aa");


    public static TheoryData<string> InvalidNames => ["", "   ", new string('n', 101)];

    // A tenant always has a plan: its creation names one.
    [Fact]
    public void CreateTenant_Valid_IsOnThePlan()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", PlanId, CreatedAt);

        tenant.Value.PlanId.ShouldBe(PlanId);
    }

    // Settings › General shows when the tenant was created.
    [Fact]
    public void CreateTenant_Valid_KeepsWhenItWasCreated()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", PlanId, CreatedAt);

        tenant.Value.CreatedAt.ShouldBe(CreatedAt);
    }

    [Theory]
    [InlineData("acme")]
    [InlineData("acme-corp")]
    [InlineData("a1b")]
    [InlineData("tenant-2024-eu")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk")]
    public void CreateTenant_UrlSafeSlug_IsCreatedWithThatSlug(string slug)
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", slug, PlanId, CreatedAt);

        tenant.IsSuccess.ShouldBeTrue();
        tenant.Value.Slug.ShouldBe(slug);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl")]
    [InlineData("Acme")]
    [InlineData("acme corp")]
    [InlineData("acme_corp")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("acme--corp")]
    [InlineData("acmé")]
    public void CreateTenant_SlugNotUrlSafe_IsRejected(string slug)
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", slug, PlanId, CreatedAt);

        tenant.IsSuccess.ShouldBeFalse();
        tenant.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantSlugInvalid);
    }

    [Fact]
    public void CreateTenant_NameWithSpacesAround_IsKeptWithoutThem()
    {
        var tenant = Tenant.Create(Id, "  Acme Ltd ", "acme", PlanId, CreatedAt);

        tenant.Value.Name.ShouldBe("Acme Ltd");
    }

    [Fact]
    public void CreateTenant_NameOfOneHundredCharacters_IsAccepted()
    {
        var name = new string('n', 100);

        var tenant = Tenant.Create(Id, name, "acme", PlanId, CreatedAt);

        tenant.Value.Name.ShouldBe(name);
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void CreateTenant_InvalidName_IsRejected(string name)
    {
        var tenant = Tenant.Create(Id, name, "acme", PlanId, CreatedAt);

        tenant.IsSuccess.ShouldBeFalse();
        tenant.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNameInvalid);
    }

    [Fact]
    public void CreateTenant_New_IsActive()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", PlanId, CreatedAt).Value;

        tenant.Status.ShouldBe(TenantStatus.Active);
    }

    [Fact]
    public void SuspendTenant_Active_IsSuspended()
    {
        var tenant = Active();

        var suspended = tenant.Suspend(Reason);

        suspended.Value.ShouldBe(StateChange.Changed);
        tenant.Status.ShouldBe(TenantStatus.Suspended);
    }

    // Suspending sets a state: a repeat changes nothing and reports no change, so nothing is announced twice.
    [Fact]
    public void SuspendTenant_Suspended_StaysSuspendedWithoutAChange()
    {
        var tenant = Active();
        tenant.Suspend(Reason);

        var suspended = tenant.Suspend(Reason);

        suspended.Value.ShouldBe(StateChange.Unchanged);
        tenant.Status.ShouldBe(TenantStatus.Suspended);
    }

    [Fact]
    public void ReactivateTenant_Suspended_IsActive()
    {
        var tenant = Active();
        tenant.Suspend(Reason);

        var reactivated = tenant.Reactivate();

        reactivated.Value.ShouldBe(StateChange.Changed);
        tenant.Status.ShouldBe(TenantStatus.Active);
    }

    [Fact]
    public void ReactivateTenant_Active_StaysActiveWithoutAChange()
    {
        var tenant = Active();

        var reactivated = tenant.Reactivate();

        reactivated.Value.ShouldBe(StateChange.Unchanged);
        tenant.Status.ShouldBe(TenantStatus.Active);
    }

    [Fact]
    public void SuspendTenant_Active_KeepsTheReasonWithoutTheSpacesAround()
    {
        var tenant = Active();

        tenant.Suspend("  Unpaid invoices ");

        tenant.SuspensionReason.ShouldBe("Unpaid invoices");
    }

    // A repeat changes nothing, its reason included.
    [Fact]
    public void SuspendTenant_Suspended_KeepsTheFirstReason()
    {
        var tenant = Active();
        tenant.Suspend(Reason);

        tenant.Suspend("Another reason");

        tenant.SuspensionReason.ShouldBe(Reason);
    }

    [Fact]
    public void ReactivateTenant_Suspended_ForgetsTheReason()
    {
        var tenant = Active();
        tenant.Suspend(Reason);

        tenant.Reactivate();

        tenant.SuspensionReason.ShouldBeNull();
    }

    // A closed tenant comes back only through a reopening.
    [Fact]
    public void SuspendOrReactivateTenant_Closed_IsRejected()
    {
        var tenant = Closed();

        var suspended = tenant.Suspend(Reason);
        var reactivated = tenant.Reactivate();

        suspended.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
        reactivated.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
        tenant.Status.ShouldBe(TenantStatus.Closed);
    }

    [Fact]
    public void RenameTenant_ANewName_IsKeptWithoutTheSpacesAroundAndTheSlugStays()
    {
        var tenant = Active();

        var renamed = tenant.Rename("  Globex Corp ");

        renamed.Value.ShouldBe(StateChange.Changed);
        tenant.Name.ShouldBe("Globex Corp");
        tenant.Slug.ShouldBe("acme");
    }

    [Fact]
    public void RenameTenant_Suspended_IsRenamed()
    {
        var tenant = Active();
        tenant.Suspend(Reason);

        var renamed = tenant.Rename("Globex Corp");

        renamed.Value.ShouldBe(StateChange.Changed);
        tenant.Name.ShouldBe("Globex Corp");
    }

    // Renaming sets a state: the name the tenant has changes nothing, also with spaces around it.
    [Theory]
    [InlineData("Acme Ltd")]
    [InlineData(" Acme Ltd  ")]
    public void RenameTenant_TheNameItHas_ChangesNothing(string name)
    {
        var tenant = Active();

        var renamed = tenant.Rename(name);

        renamed.Value.ShouldBe(StateChange.Unchanged);
        tenant.Name.ShouldBe("Acme Ltd");
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void RenameTenant_InvalidName_IsRejected(string name)
    {
        var tenant = Active();

        var renamed = tenant.Rename(name);

        renamed.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNameInvalid);
        tenant.Name.ShouldBe("Acme Ltd");
    }

    [Theory]
    [InlineData("Closed")]
    [InlineData("Purging")]
    public void RenameTenant_NeitherActiveNorSuspended_IsRejected(string status)
    {
        var tenant = InStatus(status);

        var renamed = tenant.Rename("Globex Corp");

        renamed.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
        tenant.Name.ShouldBe("Acme Ltd");
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    public void CloseTenant_ActiveOrSuspended_IsClosedAtThatMoment(string status)
    {
        var tenant = InStatus(status);

        var closed = tenant.Close(Now, []);

        closed.Value.Change.ShouldBe(StateChange.Changed);
        tenant.Status.ShouldBe(TenantStatus.Closed);
        tenant.ClosedAt.ShouldBe(Now);
    }

    // The reason stays until the tenant is reopened.
    [Fact]
    public void CloseTenant_Suspended_KeepsTheReason()
    {
        var tenant = Active();
        tenant.Suspend(Reason);

        tenant.Close(Now, []);

        tenant.SuspensionReason.ShouldBe(Reason);
    }

    // Closing sets a state: a repeat changes nothing, the moment of the closure included.
    [Fact]
    public void CloseTenant_Closed_ChangesNothingAndKeepsTheFirstMoment()
    {
        var tenant = Closed();

        var closed = tenant.Close(Now.AddDays(1), []);

        closed.Value.Change.ShouldBe(StateChange.Unchanged);
        tenant.ClosedAt.ShouldBe(Now);
    }

    // K6h: the closure cancels every pending invitation, expired ones included, in the same change.
    [Fact]
    public void CloseTenant_WithPendingInvitations_CancelsEachOne()
    {
        var pending = PendingTo("ada@example.com", RoleOf("Member"));
        var expired = Invitation.Create(
            Guid.NewGuid(), Id, EmailAddress.Create("grace@example.com"), RoleOf("Owner"), InviterId, CreatedAt, Lifetime, InvitationSecret.Generate(), Language.En);

        var closed = Active().Close(Now, [pending, expired]);

        closed.Value.Cancelled.ShouldBe([pending, expired]);
        pending.Status.ShouldBe(InvitationStatus.Cancelled);
        expired.Status.ShouldBe(InvitationStatus.Cancelled);
    }

    [Fact]
    public void CloseTenant_ClosedWithAPendingInvitation_CancelsNothing()
    {
        var pending = PendingTo("ada@example.com", RoleOf("Member"));

        var closed = Closed().Close(Now, [pending]);

        closed.Value.Cancelled.ShouldBeEmpty();
        pending.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public void RevokeInvitation_APendingInvitation_CancelsIt()
    {
        var invitation = PendingTo("ada@example.com", RoleOf("Member"));

        var revoked = Active().Revoke(RoleOf("Owner"), invitation, Roles);

        revoked.Value.ShouldBe(StateChange.Changed);
        invitation.Status.ShouldBe(InvitationStatus.Cancelled);
    }

    [Fact]
    public void RevokeInvitation_NoInvitation_IsNotFound()
    {
        var revoked = Active().Revoke(RoleOf("Owner"), null, Roles);

        revoked.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationNotFound);
        revoked.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public void RevokeInvitation_AnotherTenantsInvitation_IsNotFoundAndStaysPending()
    {
        var other = Invitation.Create(
            Guid.NewGuid(), Guid.NewGuid(), EmailAddress.Create("ada@example.com"), RoleOf("Member"), InviterId, Now, Lifetime, InvitationSecret.Generate(), Language.En);

        var revoked = Active().Revoke(RoleOf("Owner"), other, Roles);

        revoked.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationNotFound);
        other.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public void RevokeInvitation_AnAcceptedInvitation_IsAConflict()
    {
        var invitation = PendingTo("ada@example.com", RoleOf("Member"));
        invitation.Accept(Verified("ada@example.com"), Guid.NewGuid(), Now);

        var revoked = Active().Revoke(RoleOf("Owner"), invitation, Roles);

        revoked.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationNotPending);
    }

    [Fact]
    public void ReopenTenant_ClosedWithinThePeriod_IsActiveAndForgetsTheClosureAndTheReason()
    {
        var tenant = Active();
        tenant.Suspend(Reason);
        tenant.Close(Now, []);

        var reopened = tenant.Reopen(Now + ReopenPeriod - TimeSpan.FromSeconds(1), ReopenPeriod);

        reopened.Value.ShouldBe(StateChange.Changed);
        tenant.Status.ShouldBe(TenantStatus.Active);
        tenant.ClosedAt.ShouldBeNull();
        tenant.SuspensionReason.ShouldBeNull();
    }

    // The period ends at the moment of the closure plus the period: from then on the tenant stays closed.
    [Fact]
    public void ReopenTenant_WhenThePeriodEnds_IsRejected()
    {
        var tenant = Closed();

        var reopened = tenant.Reopen(Now + ReopenPeriod, ReopenPeriod);

        reopened.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantReopenPeriodEnded);
        tenant.Status.ShouldBe(TenantStatus.Closed);
        tenant.ClosedAt.ShouldBe(Now);
    }

    // Reopening sets a state: an active tenant, which a first reopening may have made active, changes nothing.
    [Fact]
    public void ReopenTenant_Active_ChangesNothing()
    {
        var tenant = Active();

        var reopened = tenant.Reopen(Now, ReopenPeriod);

        reopened.Value.ShouldBe(StateChange.Unchanged);
        tenant.Status.ShouldBe(TenantStatus.Active);
    }

    [Theory]
    [InlineData("Suspended")]
    [InlineData("Purging")]
    public void ReopenTenant_NeitherClosedNorActive_IsRejected(string status)
    {
        var tenant = InStatus(status);

        var reopened = tenant.Reopen(Now, ReopenPeriod);

        reopened.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotClosed);
        tenant.Status.ToString().ShouldBe(status);
    }

    [Fact]
    public void CheckMemberAccess_Active_IsAllowed()
    {
        var allowed = Tenant.CheckMemberAccess(TenantStatus.Active);

        allowed.IsSuccess.ShouldBeTrue();
    }

    // A member is told why: the tenant door, and every tenant command under the tenant's lock, refuse a tenant that is not active.
    [Theory]
    [InlineData("Suspended", "tenant.suspended")]
    [InlineData("Closed", "tenant.closed")]
    public void CheckMemberAccess_SuspendedOrClosed_IsForbiddenWithTheReason(string status, string code)
    {
        var allowed = Tenant.CheckMemberAccess(Enum.Parse<TenantStatus>(status));

        allowed.Error.Type.ShouldBe(ErrorType.Forbidden);
        allowed.Error.Code.ToString().ShouldBe(code);
    }

    // The purge's start and its end refuse a member as a closure does: the tenant door, and every tenant command under the lock (K4).
    [Theory]
    [InlineData("Purging")]
    [InlineData("Purged")]
    public void CheckMemberAccess_PurgingOrPurged_IsForbiddenAsClosed(string status)
    {
        var allowed = Tenant.CheckMemberAccess(Enum.Parse<TenantStatus>(status));

        allowed.Error.Type.ShouldBe(ErrorType.Forbidden);
        allowed.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantClosed);
    }

    // The pivot of the purge (S5): a tenant still closed at the closure the purge was scheduled for starts it, and can no longer be
    // reopened.
    [Fact]
    public void StartTenantPurge_ClosedAtTheScheduledClosure_IsPurging()
    {
        var tenant = Closed();

        var started = tenant.BeginPurge(Now);

        started.ShouldBe(StateChange.Changed);
        tenant.Status.ShouldBe(TenantStatus.Purging);
        tenant.ClosedAt.ShouldBe(Now);
    }

    // PostgreSQL keeps a moment to the microsecond and .NET to 100 ns, so a closure read back from the database is the closure the
    // message carries, less the ticks under a microsecond.
    [Fact]
    public void StartTenantPurge_TheScheduledClosureWithTicksUnderAMicrosecond_IsPurging()
    {
        var tenant = Closed();

        var started = tenant.BeginPurge(Now + TimeSpan.FromTicks(7));

        started.ShouldBe(StateChange.Changed);
        tenant.Status.ShouldBe(TenantStatus.Purging);
    }

    // A tenant reopened and closed again carries a new closure: the first closure's purge does nothing, and its own purge comes later.
    [Fact]
    public void StartTenantPurge_ClosedAtAnotherMoment_ChangesNothing()
    {
        var tenant = Closed();

        var started = tenant.BeginPurge(Now - TimeSpan.FromDays(1));

        started.ShouldBe(StateChange.Unchanged);
        tenant.Status.ShouldBe(TenantStatus.Closed);
    }

    // A tenant reopened before the purge is active again: the scheduled purge does nothing. A purge that has started changes nothing.
    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    [InlineData("Purging")]
    public void StartTenantPurge_NotClosed_ChangesNothing(string status)
    {
        var tenant = InStatus(status);

        var started = tenant.BeginPurge(Now);

        started.ShouldBe(StateChange.Unchanged);
        tenant.Status.ToString().ShouldBe(status);
    }

    // K4: the tombstone keeps the id, the name, the slug, the status, the moment of the creation and the moment of the purge; the
    // reason of a suspension and the moment of the closure go.
    [Fact]
    public void PurgeTenant_Purging_KeepsOnlyTheTombstone()
    {
        var tenant = Active();
        tenant.Suspend(Reason);
        tenant.Close(Now, []);
        tenant.BeginPurge(Now);
        var purgedAt = Now + TimeSpan.FromDays(30);

        var purged = tenant.Purge(purgedAt);

        purged.ShouldBe(StateChange.Changed);
        (tenant.Id, tenant.Name, tenant.Slug, tenant.Status, tenant.CreatedAt, tenant.PurgedAt).ShouldBe((Id, "Acme Ltd", "acme", TenantStatus.Purged, CreatedAt, purgedAt));
        (tenant.SuspensionReason, tenant.ClosedAt).ShouldBe((null, null));
    }

    // The purge's last step may run twice (S7): a tombstone stays as it is.
    [Fact]
    public void PurgeTenant_Purged_ChangesNothing()
    {
        var tenant = InStatus("Purged");

        var purged = tenant.Purge(Now + TimeSpan.FromDays(1));

        purged.ShouldBe(StateChange.Unchanged);
        tenant.PurgedAt.ShouldBe(Now);
    }

    // Only the purge's pivot leads to its last step: a tenant whose purge has not started is a fault there, not an answer.
    [Theory]
    [InlineData("Active")]
    [InlineData("Closed")]
    public void PurgeTenant_PurgeNotStarted_Throws(string status)
    {
        var tenant = InStatus(status);

        Should.Throw<InvalidOperationException>(() => tenant.Purge(Now));
        tenant.Status.ToString().ShouldBe(status);
    }

    [Fact]
    public void CloseTenant_Purging_IsRejected()
    {
        var tenant = InStatus("Purging");

        var closed = tenant.Close(Now, []);

        closed.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
        tenant.Status.ShouldBe(TenantStatus.Purging);
    }

    [Fact]
    public void SuspendTenant_Purging_IsRejected()
    {
        var tenant = InStatus("Purging");

        var suspended = tenant.Suspend(Reason);

        suspended.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
        tenant.Status.ShouldBe(TenantStatus.Purging);
    }

    [Fact]
    public void ReactivateTenant_Purging_IsRejected()
    {
        var tenant = InStatus("Purging");

        var reactivated = tenant.Reactivate();

        reactivated.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
        tenant.Status.ShouldBe(TenantStatus.Purging);
    }

    [Fact]
    public void ReadReopenableUntil_AClosedTenant_IsItsClosurePlusThePeriod()
    {
        var tenant = Closed();

        tenant.ReopenableUntil(ReopenPeriod).ShouldBe(Now + ReopenPeriod);
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    public void ReadReopenableUntil_ATenantThatIsNotClosed_IsNone(string status)
    {
        var tenant = InStatus(status);

        tenant.ReopenableUntil(ReopenPeriod).ShouldBeNull();
    }

    // The warning comes at the closure plus the reopen period, less the warning's lead, for a closed tenant and for one whose purge began.
    [Theory]
    [InlineData("Closed")]
    [InlineData("Purging")]
    public void ReadPurgeWarningAt_AClosedOrAPurgingTenant_IsItsClosurePlusThePeriodLessTheLead(string status)
    {
        var tenant = InStatus(status);

        tenant.PurgeWarningAt(ReopenPeriod, TimeSpan.FromDays(7)).ShouldBe(Now + TimeSpan.FromDays(23));
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    [InlineData("Purged")]
    public void ReadPurgeWarningAt_ATenantThatIsNeitherClosedNorPurging_IsNone(string status)
    {
        var tenant = InStatus(status);

        tenant.PurgeWarningAt(ReopenPeriod, TimeSpan.FromDays(7)).ShouldBeNull();
    }

    [Fact]
    public void InviteOwner_AnActiveTenantWithoutAnOwner_CreatesAPendingOwnerInvitation()
    {
        var invited = Active().Invite(Platform, Request("ada@example.com", RoleOf("Owner")), Facts(owners: 0), DailyLimit);

        invited.Value.Invitation.Email.ShouldBe("ada@example.com");
        invited.Value.Invitation.RoleId.ShouldBe(RoleOf("Owner").Id);
        invited.Value.Invitation.TenantId.ShouldBe(Id);
        invited.Value.Invitation.InvitedBy.ShouldBe(InviterId);
        invited.Value.Invitation.Status.ShouldBe(InvitationStatus.Pending);
        invited.Value.Replaced.ShouldBeEmpty();
    }

    [Fact]
    public void InviteOwner_ATenantWithAnOwner_IsAConflict()
    {
        var invited = Active().Invite(Platform, Request("ada@example.com", RoleOf("Owner")), Facts(owners: 1), DailyLimit);

        invited.Error.Type.ShouldBe(ErrorType.Conflict);
        invited.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantHasOwner);
    }

    [Theory]
    [InlineData("Suspended")]
    [InlineData("Closed")]
    [InlineData("Purging")]
    public void InviteOwner_ATenantThatIsNotActive_IsAConflict(string status)
    {
        var invited = InStatus(status).Invite(Platform, Request("ada@example.com", RoleOf("Owner")), Facts(owners: 0), DailyLimit);

        invited.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
    }

    // Nobody else invites into a tenant without an Owner, so every pending invitation is the platform's, and the new one replaces it,
    // also to another address.
    [Fact]
    public void InviteOwner_PendingInvitationsToOtherAddresses_CancelsEachOne()
    {
        var first = PendingTo("grace@example.com", RoleOf("Owner"));
        var second = PendingTo("ada@example.com", RoleOf("Owner"));

        var invited = Active().Invite(Platform, Request("ada@example.com", RoleOf("Owner")), Facts(owners: 0, pending: [first, second]), DailyLimit);

        invited.Value.Replaced.ShouldBe([first, second]);
        first.Status.ShouldBe(InvitationStatus.Cancelled);
        second.Status.ShouldBe(InvitationStatus.Cancelled);
    }

    // The daily limit is a member's; the platform's Owner invitation and a member's address are no rules of the platform.
    [Fact]
    public void InviteOwner_PastTheDailyLimit_IsAllowed()
    {
        var invited = Active().Invite(Platform, Request("ada@example.com", RoleOf("Owner")), Facts(owners: 0, sent: DailyLimit), DailyLimit);

        invited.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void InviteOwner_WithoutALanguage_TakesTheSystemAdmins()
    {
        var invited = Active().Invite(Platform, Request("ada@example.com", RoleOf("Owner"), language: null), Facts(owners: 0), DailyLimit);

        invited.Value.Invitation.Language.ShouldBe(Language.Tr);
    }

    [Theory]
    [InlineData("Owner", "Owner")]
    [InlineData("Owner", "Admin")]
    [InlineData("Owner", "Member")]
    [InlineData("Admin", "Admin")]
    [InlineData("Admin", "Member")]
    public void InviteMember_ARoleTheInviterMayGive_CreatesAPendingInvitationWithTheRole(string inviter, string role)
    {
        var invited = Active().Invite(MemberOf(inviter), Request("ada@example.com", RoleOf(role)), Facts(owners: 1), DailyLimit);

        invited.Value.Invitation.RoleId.ShouldBe(RoleOf(role).Id);
        invited.Value.Invitation.Status.ShouldBe(InvitationStatus.Pending);
        invited.Value.Invitation.ExpiresAt.ShouldBe(Now + Lifetime);
    }

    // The role rule comes first, so an Admin learns nothing about the address.
    [Fact]
    public void InviteMember_AnAdminForTheOwnerRoleToAMembersAddress_IsForbiddenAndCancelsNothing()
    {
        var pending = PendingTo("ada@example.com", RoleOf("Member"));

        var invited = Active().Invite(
            MemberOf("Admin"), Request("ada@example.com", RoleOf("Owner")), Facts(owners: 1, member: true, pending: [pending]), DailyLimit);

        invited.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationOwnerRequiresOwner);
        pending.Status.ShouldBe(InvitationStatus.Pending);
    }

    // The address comes before the limit.
    [Fact]
    public void InviteMember_AMembersAddressAtTheDailyLimit_IsAConflict()
    {
        var invited = Active().Invite(MemberOf("Owner"), Request("ada@example.com", RoleOf("Member")), Facts(owners: 1, member: true, sent: DailyLimit), DailyLimit);

        invited.Error.Type.ShouldBe(ErrorType.Conflict);
        invited.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberExists);
    }

    [Fact]
    public void InviteMember_AtTheDailyLimit_IsTooManyRequestsAndCancelsNothing()
    {
        var pending = PendingTo("ada@example.com", RoleOf("Member"));

        var invited = Active().Invite(MemberOf("Owner"), Request("ada@example.com", RoleOf("Member")), Facts(owners: 1, sent: DailyLimit, pending: [pending]), DailyLimit);

        invited.Error.Type.ShouldBe(ErrorType.TooManyRequests);
        invited.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationDailyLimitReached);
        pending.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public void InviteMember_OneBelowTheDailyLimit_IsAllowed()
    {
        var invited = Active().Invite(MemberOf("Owner"), Request("ada@example.com", RoleOf("Member")), Facts(owners: 1, sent: DailyLimit - 1), DailyLimit);

        invited.IsSuccess.ShouldBeTrue();
    }

    // K2: the new invitation replaces a pending one to the same address, and only that one.
    [Fact]
    public void InviteMember_APendingInvitationToTheAddress_CancelsItAndKeepsTheOthers()
    {
        var same = PendingTo("ada@example.com", RoleOf("Member"));
        var other = PendingTo("grace@example.com", RoleOf("Member"));

        var invited = Active().Invite(MemberOf("Owner"), Request("ada@example.com", RoleOf("Admin")), Facts(owners: 1, pending: [same, other]), DailyLimit);

        invited.Value.Replaced.ShouldBe([same]);
        same.Status.ShouldBe(InvitationStatus.Cancelled);
        other.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public void InviteMember_WithALanguage_KeepsIt()
    {
        var invited = Active().Invite(MemberOf("Owner"), Request("ada@example.com", RoleOf("Member"), Language.Ar), Facts(owners: 1), DailyLimit);

        invited.Value.Invitation.Language.ShouldBe(Language.Ar);
    }

    [Fact]
    public void InviteMember_WithoutALanguage_TakesTheInviters()
    {
        var invited = Active().Invite(MemberOf("Owner"), Request("ada@example.com", RoleOf("Member"), language: null), Facts(owners: 1), DailyLimit);

        invited.Value.Invitation.Language.ShouldBe(Language.Tr);
    }

    [Theory]
    [InlineData("Owner", "Owner", "Admin")]
    [InlineData("Owner", "Admin", "Owner")]
    [InlineData("Owner", "Member", "Owner")]
    [InlineData("Owner", "Admin", "Member")]
    [InlineData("Admin", "Admin", "Member")]
    [InlineData("Admin", "Member", "Admin")]
    public void ChangeMemberRole_ARoleTheActorMayGive_ChangesIt(string actor, string target, string role)
    {
        var changed = Membership(target);

        var result = Active().ChangeRole(Membership(actor), RoleOf(role), new MemberFacts(changed, 2, Roles));

        result.Value.Change.ShouldBe(StateChange.Changed);
        changed.RoleId.ShouldBe(RoleOf(role).Id);
    }

    [Theory]
    [InlineData("Owner", "Admin")]
    [InlineData("Owner", "Member")]
    [InlineData("Admin", "Owner")]
    [InlineData("Member", "Owner")]
    public void ChangeMemberRole_AnAdminOnTheOwnerRole_IsForbiddenAndChangesNothing(string target, string role)
    {
        var changed = Membership(target);

        var result = Active().ChangeRole(Membership("Admin"), RoleOf(role), new MemberFacts(changed, 2, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberOwnerRequiresOwner);
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        changed.RoleId.ShouldBe(RoleOf(target).Id);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Member")]
    public void ChangeMemberRole_TheLastOwner_IsAConflictAndKeepsTheRole(string role)
    {
        var changed = Membership("Owner");

        var result = Active().ChangeRole(Membership("Owner"), RoleOf(role), new MemberFacts(changed, 1, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberLastOwner);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        changed.RoleId.ShouldBe(RoleOf("Owner").Id);
    }

    // K1: an Owner steps down when another Owner exists, and cannot as the last one.
    [Fact]
    public void ChangeMemberRole_AnOwnerStepsDownWithAnotherOwner_ChangesIt()
    {
        var self = Membership("Owner");

        var result = Active().ChangeRole(self, RoleOf("Admin"), new MemberFacts(self, 2, Roles));

        result.Value.Change.ShouldBe(StateChange.Changed);
        self.RoleId.ShouldBe(RoleOf("Admin").Id);
    }

    [Fact]
    public void ChangeMemberRole_TheLastOwnerStepsDown_IsAConflict()
    {
        var self = Membership("Owner");

        var result = Active().ChangeRole(self, RoleOf("Admin"), new MemberFacts(self, 1, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberLastOwner);
        self.RoleId.ShouldBe(RoleOf("Owner").Id);
    }

    // K1: an Admin may become a Member, and cannot make themselves an Owner.
    [Fact]
    public void ChangeMemberRole_AnAdminBecomesAMember_ChangesIt()
    {
        var self = Membership("Admin");

        var result = Active().ChangeRole(self, RoleOf("Member"), new MemberFacts(self, 1, Roles));

        result.Value.Change.ShouldBe(StateChange.Changed);
    }

    [Fact]
    public void ChangeMemberRole_AnAdminMakesThemselvesAnOwner_IsForbidden()
    {
        var self = Membership("Admin");

        var result = Active().ChangeRole(self, RoleOf("Owner"), new MemberFacts(self, 1, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberOwnerRequiresOwner);
        self.RoleId.ShouldBe(RoleOf("Admin").Id);
    }

    // K1 and I1: the role the member already has changes nothing. The last Owner keeps the Owner role, which is no loss.
    [Theory]
    [InlineData("Owner", 1)]
    [InlineData("Admin", 1)]
    [InlineData("Member", 1)]
    public void ChangeMemberRole_TheRoleTheMemberHas_ChangesNothing(string role, int owners)
    {
        var result = Active().ChangeRole(Membership("Owner"), RoleOf(role), new MemberFacts(Membership(role), owners, Roles));

        result.Value.Change.ShouldBe(StateChange.Unchanged);
    }

    // docs/flows/member-management.md: an Admin who acts on an Owner is refused before the Owner count is looked at.
    [Fact]
    public void ChangeMemberRole_AnAdminOnTheLastOwner_IsForbiddenFirst()
    {
        var result = Active().ChangeRole(Membership("Admin"), RoleOf("Member"), new MemberFacts(Membership("Owner"), 1, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberOwnerRequiresOwner);
    }

    [Theory]
    [InlineData("Owner", "Owner")]
    [InlineData("Owner", "Admin")]
    [InlineData("Owner", "Member")]
    [InlineData("Admin", "Admin")]
    [InlineData("Admin", "Member")]
    public void RemoveMember_AMemberTheActorMayRemove_IsAllowed(string actor, string target)
    {
        var result = Active().Remove(Membership(actor), new MemberFacts(Membership(target), 2, Roles));

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    public void RemoveMember_Themselves_IsForbidden(string role)
    {
        var self = Membership(role);

        var result = Active().Remove(self, new MemberFacts(self, 2, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberCannotRemoveSelf);
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public void RemoveMember_AnAdminRemovesAnOwner_IsForbidden()
    {
        var result = Active().Remove(Membership("Admin"), new MemberFacts(Membership("Owner"), 2, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberOwnerRequiresOwner);
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    // Only the count of Owners decides: a tenant whose Owner count is one keeps that Owner, whoever acts.
    [Fact]
    public void RemoveMember_TheLastOwner_IsAConflict()
    {
        var result = Active().Remove(Membership("Owner"), new MemberFacts(Membership("Owner"), 1, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberLastOwner);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    // docs/flows/member-management.md: removing yourself is refused before any other check, also for the last Owner.
    [Fact]
    public void RemoveMember_TheLastOwnerThemselves_IsForbiddenFirst()
    {
        var self = Membership("Owner");

        var result = Active().Remove(self, new MemberFacts(self, 1, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberCannotRemoveSelf);
    }

    [Fact]
    public void ChangeMemberRole_ARoleChange_AnswersTheMemberAndTheRoleTheyHad()
    {
        var target = Membership("Admin");

        var result = Active().ChangeRole(Membership("Owner"), RoleOf("Member"), new MemberFacts(target, 1, Roles));

        result.Value.Member.ShouldBeSameAs(target);
        result.Value.OldRole.ShouldBe(RoleOf("Admin"));
    }

    // Row level security hides another tenant's members, so the target is not found.
    [Fact]
    public void ChangeMemberRole_NoTargetMembership_IsNotFound()
    {
        var result = Active().ChangeRole(Membership("Owner"), RoleOf("Member"), new MemberFacts(null, 1, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberNotFound);
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public void ChangeMemberRole_AMembershipOfAnotherTenant_IsNotFoundAndChangesNothing()
    {
        var other = new Membership(Guid.NewGuid(), Guid.NewGuid(), RoleOf("Member").Id, DateTimeOffset.UnixEpoch);

        var result = Active().ChangeRole(Membership("Owner"), RoleOf("Admin"), new MemberFacts(other, 1, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberNotFound);
        other.RoleId.ShouldBe(RoleOf("Member").Id);
    }

    [Fact]
    public void RemoveMember_AMemberTheActorMayRemove_AnswersTheMembershipToRemove()
    {
        var target = Membership("Member");

        var result = Active().Remove(Membership("Admin"), new MemberFacts(target, 1, Roles));

        result.Value.ShouldBeSameAs(target);
    }

    [Fact]
    public void RemoveMember_NoTargetMembership_IsNotFound()
    {
        var result = Active().Remove(Membership("Owner"), new MemberFacts(null, 1, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberNotFound);
    }

    [Fact]
    public void RemoveMember_AMembershipOfAnotherTenant_IsNotFound()
    {
        var other = new Membership(Guid.NewGuid(), Guid.NewGuid(), RoleOf("Member").Id, DateTimeOffset.UnixEpoch);

        var result = Active().Remove(Membership("Owner"), new MemberFacts(other, 1, Roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberNotFound);
    }

    // Owner recovery (docs/flows/owner-recovery.md): the platform makes a member an Owner, with or without an Owner there. The old Owner keeps their role.
    [Theory]
    [InlineData("Admin", 0)]
    [InlineData("Member", 0)]
    [InlineData("Admin", 1)]
    [InlineData("Member", 2)]
    public void MakeOwner_AMemberWithOrWithoutAnOwnerThere_GivesThemTheOwnerRole(string role, int owners)
    {
        var target = Membership(role);

        var result = Active().MakeOwner(RoleOf("Owner"), new MemberFacts(target, owners, Roles), UserStatus.Active);

        (result.Value.Change, result.Value.Member, result.Value.OldRole).ShouldBe((StateChange.Changed, target, RoleOf(role)));
        target.RoleId.ShouldBe(RoleOf("Owner").Id);
    }

    [Fact]
    public void MakeOwner_AMemberOfACustomRole_GivesThemTheOwnerRole()
    {
        var billing = Custom("Billing", ProfileRead, MembersRead);
        var target = Membership(billing);

        var result = Active().MakeOwner(RoleOf("Owner"), new MemberFacts(target, 0, RolesWith(billing)), UserStatus.Active);

        result.Value.OldRole.ShouldBe(billing);
        target.RoleId.ShouldBe(RoleOf("Owner").Id);
    }

    // The rule InviteOwner asks: only an active tenant gets an Owner from the platform. It comes first.
    [Theory]
    [InlineData("Suspended")]
    [InlineData("Closed")]
    [InlineData("Purging")]
    public void MakeOwner_ATenantThatIsNotActive_IsAConflictAndChangesNothing(string status)
    {
        var target = Membership("Member");

        var result = InStatus(status).MakeOwner(RoleOf("Owner"), new MemberFacts(target, 0, Roles), UserStatus.Suspended);

        (result.Error.Code, result.Error.Type).ShouldBe((ControlPlaneErrorCodes.TenantNotActive, ErrorType.Conflict));
        target.RoleId.ShouldBe(RoleOf("Member").Id);
    }

    // Row level security hides another tenant's members, so the target is not found; an id no user has is no member either.
    [Fact]
    public void MakeOwner_NoMembership_IsNotFound()
    {
        var result = Active().MakeOwner(RoleOf("Owner"), new MemberFacts(null, 1, Roles), userStatus: null);

        (result.Error.Code, result.Error.Type).ShouldBe((ControlPlaneErrorCodes.MemberNotFound, ErrorType.NotFound));
    }

    [Fact]
    public void MakeOwner_AMembershipOfAnotherTenant_IsNotFound()
    {
        var other = new Membership(Guid.NewGuid(), Guid.NewGuid(), RoleOf("Member").Id, DateTimeOffset.UnixEpoch);

        var result = Active().MakeOwner(RoleOf("Owner"), new MemberFacts(other, 1, Roles), UserStatus.Active);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberNotFound);
    }

    // A member whose role holds tenant.owners.manage is an Owner already (A1). It is no state to set again: the action makes an Owner.
    [Fact]
    public void MakeOwner_AnOwner_IsAConflict()
    {
        var target = Membership("Owner");

        var result = Active().MakeOwner(RoleOf("Owner"), new MemberFacts(target, 1, Roles), UserStatus.Suspended);

        (result.Error.Code, result.Error.Type).ShouldBe((ControlPlaneErrorCodes.MemberAlreadyOwner, ErrorType.Conflict));
    }

    // A suspended user is refused at the tenant door (A9), and a deleted one has no names (A8): neither can manage the tenant.
    [Theory]
    [InlineData("Suspended", "user.suspended")]
    [InlineData("Deleted", "user.deleted")]
    public void MakeOwner_AUserWhoIsNotActive_IsAConflictAndChangesNothing(string status, string code)
    {
        var target = Membership("Member");

        var result = Active().MakeOwner(RoleOf("Owner"), new MemberFacts(target, 0, Roles), Enum.Parse<UserStatus>(status));

        (result.Error.Code.Value, result.Error.Type).ShouldBe((code, ErrorType.Conflict));
        target.RoleId.ShouldBe(RoleOf("Member").Id);
    }

    // docs/flows/user-deletion.md: the tenant's last Owner stays, and the error names the tenant.
    [Fact]
    public void RemoveUserMembership_TheLastOwner_IsAConflictThatNamesTheTenant()
    {
        var result = Active().RemoveForUserDeletion(new MemberFacts(Membership("Owner"), 1, Roles), []);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.UserLastOwner);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.TenantIds.ShouldBe([Id]);
    }

    [Theory]
    [InlineData("Owner", 2)]
    [InlineData("Admin", 1)]
    [InlineData("Member", 1)]
    public void RemoveUserMembership_AMembershipThatMayGo_AnswersIt(string role, int owners)
    {
        var membership = Membership(role);

        var result = Active().RemoveForUserDeletion(new MemberFacts(membership, owners, Roles), []);

        result.Value!.Membership.ShouldBeSameAs(membership);
    }

    [Fact]
    public void RemoveUserMembership_AUserWhoIsNoLongerAMember_AnswersNone()
    {
        var result = Active().RemoveForUserDeletion(new MemberFacts(null, 1, Roles), []);

        result.Value.ShouldBeNull();
    }

    // A8: a pending invitation always has an address, so it is cancelled before its address goes.
    [Fact]
    public void RemoveUserMembership_InvitationsOfTheUser_CancelsThePendingOneAndErasesEveryAddress()
    {
        var pending = PendingTo("ada@example.com", RoleOf("Member"));
        var cancelled = PendingTo("ada@example.com", RoleOf("Member"));
        cancelled.Cancel();

        var removed = Active().RemoveForUserDeletion(new MemberFacts(Membership("Member"), 1, Roles), [pending, cancelled]);

        removed.Value!.Cancelled.ShouldBe([pending]);
        (pending.Status, pending.Email, cancelled.Status, cancelled.Email).ShouldBe((InvitationStatus.Cancelled, null, InvitationStatus.Cancelled, null));
    }

    // The last Owner stays, and so does every address of the tenant's invitations (W7).
    [Fact]
    public void RemoveUserMembership_TheLastOwner_ErasesNoAddress()
    {
        var cancelled = PendingTo("ada@example.com", RoleOf("Member"));
        cancelled.Cancel();

        var removed = Active().RemoveForUserDeletion(new MemberFacts(Membership("Owner"), 1, Roles), [cancelled]);

        removed.Error.Code.ShouldBe(ControlPlaneErrorCodes.UserLastOwner);
        cancelled.Email.ShouldBe("ada@example.com");
    }

    [Fact]
    public void RemoveUserMembership_AnInvitationOfAnotherTenant_IsNeverErased()
    {
        var invitation = Invitation.Create(
            Guid.NewGuid(), Guid.NewGuid(), EmailAddress.Create("ada@example.com"), RoleOf("Member"), InviterId, Now, Lifetime, InvitationSecret.Generate(), Language.En);

        var erase = () => Active().RemoveForUserDeletion(new MemberFacts(Membership("Member"), 1, Roles), [invitation]);

        erase.ShouldThrow<InvalidOperationException>();
        invitation.Email.ShouldBe("ada@example.com");
    }

    // A8: a tenant the deleted user left before erases their address by the same rule, without a membership to remove.
    [Fact]
    public void EraseFormerMember_InvitationsOfTheUser_CancelsThePendingOneAndErasesEveryAddress()
    {
        var pending = PendingTo("ada@example.com", RoleOf("Member"));
        var cancelled = PendingTo("ada@example.com", RoleOf("Member"));
        cancelled.Cancel();

        var cancelledNow = Active().EraseFormerMember(null, [pending, cancelled]);

        cancelledNow.ShouldBe([pending]);
        (pending.Status, pending.Email, cancelled.Status, cancelled.Email).ShouldBe((InvitationStatus.Cancelled, null, InvitationStatus.Cancelled, null));
    }

    // A message that arrives twice cancels nothing the second time, so no sign-up is withdrawn twice.
    [Fact]
    public void EraseFormerMember_Twice_CancelsNothingTheSecondTime()
    {
        var pending = PendingTo("ada@example.com", RoleOf("Member"));
        var tenant = Active();
        tenant.EraseFormerMember(null, [pending]);

        var cancelledAgain = tenant.EraseFormerMember(null, [pending]);

        cancelledAgain.ShouldBeEmpty();
        (pending.Status, pending.Email).ShouldBe((InvitationStatus.Cancelled, null));
    }

    // A deleted user is a member of no tenant (A8): a member's invitations keep their addresses.
    [Fact]
    public void EraseFormerMember_AUserWhoIsAMember_IsRefusedAndErasesNothing()
    {
        var invitation = PendingTo("ada@example.com", RoleOf("Member"));

        var erase = () => Active().EraseFormerMember(Membership("Member"), [invitation]);

        erase.ShouldThrow<InvalidOperationException>();
        (invitation.Status, invitation.Email).ShouldBe((InvitationStatus.Pending, "ada@example.com"));
    }

    [Fact]
    public void EraseFormerMember_AnInvitationOfAnotherTenant_IsNeverErased()
    {
        var invitation = Invitation.Create(
            Guid.NewGuid(), Guid.NewGuid(), EmailAddress.Create("ada@example.com"), RoleOf("Member"), InviterId, Now, Lifetime, InvitationSecret.Generate(), Language.En);

        var erase = () => Active().EraseFormerMember(null, [invitation]);

        erase.ShouldThrow<InvalidOperationException>();
        invitation.Email.ShouldBe("ada@example.com");
    }

    [Theory]
    [InlineData("Owner", 1, false)]
    [InlineData("Owner", 2, true)]
    [InlineData("Admin", 1, true)]
    public void DeleteUser_TheCheckOfAMembership_RefusesOnlyTheLastOwner(string role, int owners, bool allowed)
    {
        var result = Tenant.CheckUserDeletion(new MemberFacts(Membership(role), owners, Roles));

        result.IsSuccess.ShouldBe(allowed);
    }

    [Fact]
    public void DeleteUser_TheCheckOfATenantTheUserIsNotAMemberOf_IsAllowed()
    {
        var result = Tenant.CheckUserDeletion(new MemberFacts(null, 0, Roles));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void AcceptInvitation_APendingInvitation_IsFound()
    {
        var found = Active().CheckInvitationFound(PendingTo("ada@example.com", RoleOf("Member")));

        found.IsSuccess.ShouldBeTrue();
    }

    // K2: a cancelled invitation answers as one that does not exist, as a revoked one does.
    [Fact]
    public void AcceptInvitation_ACancelledInvitation_IsNotFound()
    {
        var invitation = PendingTo("ada@example.com", RoleOf("Member"));
        invitation.Cancel();

        var found = Active().CheckInvitationFound(invitation);

        found.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationNotFound);
        found.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public void AcceptInvitation_TheInvitedPerson_JoinsWithTheInvitationsRoleAndUsesItUp()
    {
        var invitation = PendingTo("ada@example.com", RoleOf("Admin"));
        var userId = Guid.NewGuid();

        var membership = Active().Accept(invitation, NewPerson, Verified("ada@example.com"), userId, Now);

        membership.Value.TenantId.ShouldBe(Id);
        membership.Value.UserId.ShouldBe(userId);
        membership.Value.RoleId.ShouldBe(RoleOf("Admin").Id);
        membership.Value.JoinedAt.ShouldBe(Now);
        invitation.Status.ShouldBe(InvitationStatus.Accepted);
        invitation.AcceptedBy.ShouldBe(userId);
    }

    // docs/flows/tenant-lifecycle.md: the order of the answers. Each case also fails every check after it.
    [Fact]
    public void AcceptInvitation_ADeletedUser_IsForbiddenFirstAndChangesNothing()
    {
        var invitation = PendingTo("ada@example.com", RoleOf("Member"));

        var membership = Suspended().Accept(invitation, new AcceptanceFacts(true, true, true), Verified("grace@example.com"), Guid.NewGuid(), Now);

        membership.Error.Code.ShouldBe(ControlPlaneErrorCodes.UserDeleted);
        membership.Error.Type.ShouldBe(ErrorType.Forbidden);
        invitation.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public void AcceptInvitation_ASystemAdmin_IsUnprocessableBeforeTheMembership()
    {
        var invitation = PendingTo("ada@example.com", RoleOf("Member"));

        var membership = Suspended().Accept(invitation, new AcceptanceFacts(false, true, true), Verified("grace@example.com"), Guid.NewGuid(), Now);

        membership.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationSystemAdminCannotJoin);
        membership.Error.Type.ShouldBe(ErrorType.Unprocessable);
    }

    [Fact]
    public void AcceptInvitation_AMember_IsAConflictBeforeTheInvitationsChecks()
    {
        var invitation = PendingTo("ada@example.com", RoleOf("Member"));

        var membership = Suspended().Accept(invitation, new AcceptanceFacts(false, false, true), Verified("grace@example.com"), Guid.NewGuid(), Now);

        membership.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberExists);
        membership.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    // The invitation's own checks come before the tenant's status, so a stranger with the link learns nothing of a suspension (T3).
    [Fact]
    public void AcceptInvitation_AnotherAddressInASuspendedTenant_IsTheInvitationsAnswer()
    {
        var invitation = PendingTo("ada@example.com", RoleOf("Member"));

        var allowed = Suspended().CheckAcceptance(invitation, NewPerson, Verified("grace@example.com"), Now);

        allowed.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationEmailMismatch);
    }

    [Fact]
    public void AcceptInvitation_ExpiredInASuspendedTenant_IsExpired()
    {
        var invitation = PendingTo("ada@example.com", RoleOf("Member"));

        var allowed = Suspended().CheckAcceptance(invitation, NewPerson, Verified("ada@example.com"), Now + Lifetime);

        allowed.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationExpired);
    }

    // The invitation stays pending, so it can be accepted once the tenant is reactivated.
    [Fact]
    public void AcceptInvitation_TheInvitedPersonInASuspendedTenant_IsAConflictAndKeepsTheInvitationPending()
    {
        var invitation = PendingTo("ada@example.com", RoleOf("Member"));

        var membership = Suspended().Accept(invitation, NewPerson, Verified("ada@example.com"), Guid.NewGuid(), Now);

        membership.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantSuspended);
        membership.Error.Type.ShouldBe(ErrorType.Conflict);
        invitation.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public void AcceptInvitation_TheInvitedPerson_IsAllowedByTheCheck()
    {
        var allowed = Active().CheckAcceptance(PendingTo("ada@example.com", RoleOf("Member")), NewPerson, Verified("ada@example.com"), Now);

        allowed.IsSuccess.ShouldBeTrue();
    }

    // Decision 2 of step r4b: the escalation rule, asked of every operation on a role. A custom actor holds exactly the permissions a
    // test names, so the cases differ by one permission.
    [Fact]
    public void CreateRole_ByAnOwner_CreatesACustomRoleWithItsTrimmedNameAndItsPermissions()
    {
        var created = Active().CreateRole(RoleOf("Owner"), NewRole("  Billing  ", ProfileRead, MembersRead, InvitationsRead), RoleFacts(), RoleLimit);

        created.Value.BuiltIn.ShouldBeNull();
        created.Value.TenantId.ShouldBe(Id);
        created.Value.Name.ShouldBe("Billing");
        created.Value.Permissions.ShouldBe([ProfileRead, MembersRead, InvitationsRead], ignoreOrder: true);
    }

    [Fact]
    public void CreateRole_AnActorWhoseRoleDoesNotHoldThePermission_IsForbidden()
    {
        var created = Active().CreateRole(RoleOf("Admin"), NewRole("Billing", MembersRead), RoleFacts(), RoleLimit);

        created.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleNotPermitted);
        created.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void CreateRole_ANameThatIsEmptyOrTooLong_IsInvalid(string name)
    {
        var created = Active().CreateRole(RoleOf("Owner"), NewRole(name, MembersRead), RoleFacts(), RoleLimit);

        created.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleNameInvalid);
        created.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void CreateRole_ANameTheTenantHas_IsAConflict()
    {
        var created = Active().CreateRole(RoleOf("Owner"), NewRole("Owner", MembersRead), RoleFacts(nameTaken: true), RoleLimit);

        created.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleNameTaken);
        created.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    // A custom role chooses from the tenant pool only (A3).
    [Theory]
    [InlineData("tenant.nothing.read")]
    [InlineData("platform.users.read")]
    public void CreateRole_APermissionOutsideTheTenantPool_IsInvalid(string permission)
    {
        var created = Active().CreateRole(RoleOf("Owner"), NewRole("Billing", MembersRead, permission), RoleFacts(), RoleLimit);

        created.Error.Code.ShouldBe(ControlPlaneErrorCodes.RolePermissionInvalid);
        created.Error.Type.ShouldBe(ErrorType.Validation);
    }

    // A3: a custom role never holds a permission of PermissionPools.OwnerOnly, not even from an Owner.
    [Fact]
    public void CreateRole_AnOwnerOnlyPermission_IsUnprocessable()
    {
        var created = Active().CreateRole(RoleOf("Owner"), NewRole("Deputy", MembersRead, PermissionPools.TenantOwnersManage), RoleFacts(), RoleLimit);

        created.Error.Code.ShouldBe(ControlPlaneErrorCodes.RolePermissionOwnerOnly);
        created.Error.Type.ShouldBe(ErrorType.Unprocessable);
    }

    [Fact]
    public void CreateRole_NoPermission_IsInvalid()
    {
        var created = Active().CreateRole(RoleOf("Owner"), NewRole("Nobody"), RoleFacts(), RoleLimit);

        created.Error.Code.ShouldBe(ControlPlaneErrorCodes.RolePermissionsRequired);
        created.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void CreateRole_AnActorWhoHoldsExactlyItsPermissions_CreatesIt()
    {
        var actor = Custom("Role keeper", PermissionPools.TenantRolesCreate, ProfileRead, MembersRead);

        var created = Active().CreateRole(actor, NewRole("Copy", PermissionPools.TenantRolesCreate, ProfileRead, MembersRead), RoleFacts(), RoleLimit);

        created.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CreateRole_APermissionTheActorDoesNotHold_IsAnEscalation()
    {
        var actor = Custom("Role keeper", PermissionPools.TenantRolesCreate, ProfileRead, MembersRead);

        var created = Active().CreateRole(actor, NewRole("Stronger", ProfileRead, MembersRead, InvitationsRead), RoleFacts(), RoleLimit);

        created.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleEscalation);
        created.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    // Decision 3 of step r4b: the limit counts custom roles only (OWASP API4).
    [Fact]
    public void CreateRole_AtTheLimit_IsAConflict()
    {
        var created = Active().CreateRole(RoleOf("Owner"), NewRole("Billing", ProfileRead, MembersRead), RoleFacts(customRoles: RoleLimit), RoleLimit);

        created.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleLimitReached);
        created.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public void CreateRole_OnePlaceBelowTheLimit_CreatesIt()
    {
        var created = Active().CreateRole(RoleOf("Owner"), NewRole("Billing", ProfileRead, MembersRead), RoleFacts(customRoles: RoleLimit - 1), RoleLimit);

        created.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void UpdateRole_ANewNameAndPermissions_ChangesThemAndAnswersWhatItAddedAndRemoved()
    {
        var role = Custom("Billing", ProfileRead, MembersRead, InvitationsRead);

        var updated = Active().UpdateRole(RoleOf("Owner"), " Finance ", [ProfileRead, MembersRead, AuditRead], RoleFacts(role));

        updated.Value.Change.ShouldBe(StateChange.Changed);
        updated.Value.OldName.ShouldBe("Billing");
        updated.Value.Added.ShouldBe([AuditRead]);
        updated.Value.Removed.ShouldBe([InvitationsRead]);
        role.Name.ShouldBe("Finance");
        role.Permissions.ShouldBe([ProfileRead, MembersRead, AuditRead], ignoreOrder: true);
    }

    [Fact]
    public void UpdateRole_TheNameAndPermissionsItHas_ChangesNothing()
    {
        var role = Custom("Billing", ProfileRead, MembersRead, InvitationsRead);

        var updated = Active().UpdateRole(RoleOf("Owner"), "Billing", [InvitationsRead, MembersRead, ProfileRead], RoleFacts(role));

        updated.Value.Change.ShouldBe(StateChange.Unchanged);
        updated.Value.Added.ShouldBeEmpty();
        updated.Value.Removed.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateRole_AnActorWhoseRoleDoesNotHoldThePermission_IsForbidden()
    {
        var updated = Active().UpdateRole(RoleOf("Admin"), "Billing", [MembersRead], RoleFacts(Custom("Billing", MembersRead)));

        updated.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleNotPermitted);
    }

    [Fact]
    public void UpdateRole_NoSuchRole_IsNotFound()
    {
        var updated = Active().UpdateRole(RoleOf("Owner"), "Billing", [MembersRead], RoleFacts());

        updated.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleNotFound);
        updated.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    // A2: a built-in role keeps its name and its permissions in code.
    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    [InlineData("Member")]
    public void UpdateRole_ABuiltInRole_IsAConflict(string builtIn)
    {
        var updated = Active().UpdateRole(RoleOf("Owner"), "Renamed", [MembersRead], RoleFacts(RoleOf(builtIn)));

        updated.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleBuiltIn);
        updated.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public void UpdateRole_ANameAnotherRoleHas_IsAConflictAndChangesNothing()
    {
        var role = Custom("Billing", MembersRead);

        var updated = Active().UpdateRole(RoleOf("Owner"), "admin", [MembersRead], RoleFacts(role, nameTaken: true));

        updated.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleNameTaken);
        role.Name.ShouldBe("Billing");
    }

    [Fact]
    public void UpdateRole_AnOwnerOnlyPermission_IsUnprocessableAndChangesNothing()
    {
        var role = Custom("Billing", MembersRead);

        var updated = Active().UpdateRole(RoleOf("Owner"), "Billing", [MembersRead, PermissionPools.TenantOwnersManage], RoleFacts(role));

        updated.Error.Code.ShouldBe(ControlPlaneErrorCodes.RolePermissionOwnerOnly);
        role.Permissions.ShouldBe([MembersRead]);
    }

    [Fact]
    public void UpdateRole_AnActorWhoHoldsExactlyThePermissionsBeforeAndAfter_ChangesIt()
    {
        var actor = Custom("Role keeper", PermissionPools.TenantRolesUpdate, ProfileRead, MembersRead, InvitationsRead);

        var updated = Active().UpdateRole(actor, "Billing", [ProfileRead, MembersRead, InvitationsRead], RoleFacts(Custom("Billing", ProfileRead, MembersRead, PermissionPools.TenantRolesUpdate)));

        updated.Value.Change.ShouldBe(StateChange.Changed);
    }

    // Before: an actor never takes from a role a permission they do not hold themselves.
    [Fact]
    public void UpdateRole_ARoleThatHoldsAPermissionTheActorDoesNot_IsAnEscalationAndChangesNothing()
    {
        var actor = Custom("Role keeper", PermissionPools.TenantRolesUpdate, ProfileRead, MembersRead);
        var role = Custom("Billing", ProfileRead, MembersRead, AuditRead);

        var updated = Active().UpdateRole(actor, "Billing", [ProfileRead, MembersRead], RoleFacts(role));

        updated.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleEscalation);
        role.Permissions.ShouldBe([ProfileRead, MembersRead, AuditRead], ignoreOrder: true);
    }

    // After: an actor never gives a role a permission they do not hold themselves.
    [Fact]
    public void UpdateRole_APermissionTheActorDoesNotHold_IsAnEscalation()
    {
        var actor = Custom("Role keeper", PermissionPools.TenantRolesUpdate, ProfileRead, MembersRead);

        var updated = Active().UpdateRole(actor, "Billing", [ProfileRead, MembersRead, AuditRead], RoleFacts(Custom("Billing", ProfileRead, MembersRead)));

        updated.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleEscalation);
    }

    // Finding 7 of step r4b and step r12: every custom role reads its tenant, and a role that acts on a role reads the roles. The rule
    // comes before the escalation rule, so even an Owner gets it.
    [Fact]
    public void CreateRole_NoProfileRead_IsInvalid()
    {
        var created = Active().CreateRole(RoleOf("Owner"), NewRole("Billing", MembersRead), RoleFacts(), RoleLimit);

        created.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleProfileReadRequired);
        created.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Theory]
    [InlineData(InvitationsCreate)]
    [InlineData(MembersUpdate)]
    [InlineData(MembersDelete)]
    [InlineData(InvitationsDelete)]
    public void CreateRole_APermissionThatActsOnARoleWithoutRolesRead_IsInvalid(string actsOnARole)
    {
        var created = Active().CreateRole(RoleOf("Owner"), NewRole("Recruiter", ProfileRead, MembersRead, actsOnARole), RoleFacts(), RoleLimit);

        created.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleRolesReadRequired);
        created.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Theory]
    [InlineData(InvitationsCreate)]
    [InlineData(MembersUpdate)]
    [InlineData(MembersDelete)]
    [InlineData(InvitationsDelete)]
    public void CreateRole_APermissionThatActsOnARoleWithRolesRead_CreatesIt(string actsOnARole)
    {
        var created = Active().CreateRole(RoleOf("Owner"), NewRole("Recruiter", ProfileRead, MembersRead, RolesRead, actsOnARole), RoleFacts(), RoleLimit);

        created.Value.Permissions.ShouldBe([ProfileRead, MembersRead, RolesRead, actsOnARole], ignoreOrder: true);
    }

    [Fact]
    public void UpdateRole_NoProfileRead_IsInvalidAndChangesNothing()
    {
        var role = Custom("Billing", ProfileRead, MembersRead);

        var updated = Active().UpdateRole(RoleOf("Owner"), "Billing", [MembersRead], RoleFacts(role));

        updated.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleProfileReadRequired);
        role.Permissions.ShouldBe([ProfileRead, MembersRead], ignoreOrder: true);
    }

    [Theory]
    [InlineData(InvitationsCreate)]
    [InlineData(MembersUpdate)]
    [InlineData(MembersDelete)]
    [InlineData(InvitationsDelete)]
    public void UpdateRole_APermissionThatActsOnARoleWithoutRolesRead_IsInvalidAndChangesNothing(string actsOnARole)
    {
        var role = Custom("Billing", ProfileRead, MembersRead);

        var updated = Active().UpdateRole(RoleOf("Owner"), "Billing", [ProfileRead, MembersRead, actsOnARole], RoleFacts(role));

        updated.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleRolesReadRequired);
        role.Permissions.ShouldBe([ProfileRead, MembersRead], ignoreOrder: true);
    }

    [Theory]
    [InlineData(InvitationsCreate)]
    [InlineData(MembersUpdate)]
    [InlineData(MembersDelete)]
    [InlineData(InvitationsDelete)]
    public void UpdateRole_APermissionThatActsOnARoleWithRolesRead_ChangesIt(string actsOnARole)
    {
        var role = Custom("Billing", ProfileRead, MembersRead);

        var updated = Active().UpdateRole(RoleOf("Owner"), "Billing", [ProfileRead, MembersRead, RolesRead, actsOnARole], RoleFacts(role));

        updated.IsSuccess.ShouldBeTrue();
        role.Permissions.ShouldBe([ProfileRead, MembersRead, RolesRead, actsOnARole], ignoreOrder: true);
    }

    [Fact]
    public void DeleteRole_ACustomRoleNobodyUses_AnswersTheRoleToDelete()
    {
        var role = Custom("Billing", MembersRead);

        var deleted = Active().DeleteRole(RoleOf("Owner"), RoleFacts(role));

        deleted.Value.ShouldBe(role);
    }

    [Fact]
    public void DeleteRole_AnActorWhoseRoleDoesNotHoldThePermission_IsForbidden()
    {
        var deleted = Active().DeleteRole(RoleOf("Admin"), RoleFacts(Custom("Billing", MembersRead)));

        deleted.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleNotPermitted);
    }

    [Fact]
    public void DeleteRole_NoSuchRole_IsNotFound()
    {
        var deleted = Active().DeleteRole(RoleOf("Owner"), RoleFacts());

        deleted.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleNotFound);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    [InlineData("Member")]
    public void DeleteRole_ABuiltInRole_IsAConflict(string builtIn)
    {
        var deleted = Active().DeleteRole(RoleOf("Owner"), RoleFacts(RoleOf(builtIn)));

        deleted.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleBuiltIn);
    }

    [Fact]
    public void DeleteRole_ARoleThatHoldsAPermissionTheActorDoesNot_IsAnEscalation()
    {
        var actor = Custom("Role keeper", PermissionPools.TenantRolesDelete, MembersRead);

        var deleted = Active().DeleteRole(actor, RoleFacts(Custom("Billing", MembersRead, AuditRead)));

        deleted.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleEscalation);
    }

    [Fact]
    public void DeleteRole_ARoleTheActorHoldsExactly_AnswersTheRoleToDelete()
    {
        var actor = Custom("Role keeper", PermissionPools.TenantRolesDelete, MembersRead);
        var role = Custom("Billing", PermissionPools.TenantRolesDelete, MembersRead);

        var deleted = Active().DeleteRole(actor, RoleFacts(role));

        deleted.Value.ShouldBe(role);
    }

    // A membership or a pending invitation that uses the role keeps it (409).
    [Fact]
    public void DeleteRole_ARoleInUse_IsAConflict()
    {
        var deleted = Active().DeleteRole(RoleOf("Owner"), RoleFacts(Custom("Billing", MembersRead), inUse: true));

        deleted.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleInUse);
        deleted.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public void InviteMember_ARoleTheInviterHoldsExactly_CreatesTheInvitation()
    {
        var inviter = Custom("Recruiter", InvitationsCreate, MembersRead);

        var invited = Active().Invite(Inviter.Member(InviterId, Language.Tr, inviter), Request("ada@example.com", Custom("Recruiter too", InvitationsCreate, MembersRead)), Facts(owners: 1), DailyLimit);

        invited.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void InviteMember_ARoleWithAPermissionTheInviterDoesNotHold_IsAnEscalationAndCancelsNothing()
    {
        var inviter = Custom("Recruiter", InvitationsCreate, MembersRead);
        var pending = PendingTo("ada@example.com", RoleOf("Member"));

        var invited = Active().Invite(Inviter.Member(InviterId, Language.Tr, inviter), Request("ada@example.com", RoleOf("Admin")), Facts(owners: 1, pending: [pending]), DailyLimit);

        invited.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleEscalation);
        pending.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public void ChangeMemberRole_RolesTheActorHoldsExactly_ChangesIt()
    {
        var actorRole = Custom("Lead", MembersUpdate, MembersRead);
        var newRole = Custom("Lead too", MembersUpdate, MembersRead);
        var oldRole = Custom("Reader", MembersRead);
        var roles = RolesWith(actorRole, newRole, oldRole);
        var target = Membership(oldRole);

        var result = Active().ChangeRole(Membership(actorRole), newRole, new MemberFacts(target, 1, roles));

        result.Value.Change.ShouldBe(StateChange.Changed);
    }

    [Fact]
    public void ChangeMemberRole_ToARoleWithAPermissionTheActorDoesNotHold_IsAnEscalationAndChangesNothing()
    {
        var actorRole = Custom("Lead", MembersUpdate, MembersRead, ProfileRead);
        var roles = RolesWith(actorRole);
        var target = Membership(RoleOf("Member"));

        var result = Active().ChangeRole(Membership(actorRole), RoleOf("Admin"), new MemberFacts(target, 1, roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleEscalation);
        target.RoleId.ShouldBe(RoleOf("Member").Id);
    }

    // Nobody acts on someone stronger: the member's current role counts too.
    [Fact]
    public void ChangeMemberRole_AMemberWhoseRoleHoldsAPermissionTheActorDoesNot_IsAnEscalationAndChangesNothing()
    {
        var actorRole = Custom("Lead", MembersUpdate, MembersRead, ProfileRead);
        var roles = RolesWith(actorRole);
        var target = Membership(RoleOf("Admin"));

        var result = Active().ChangeRole(Membership(actorRole), RoleOf("Member"), new MemberFacts(target, 1, roles));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleEscalation);
        target.RoleId.ShouldBe(RoleOf("Admin").Id);
    }

    [Fact]
    public void RemoveMember_AMemberWhoseRoleTheActorHoldsExactly_AnswersTheMembershipToRemove()
    {
        var actorRole = Custom("Lead", MembersDelete, MembersRead);
        var targetRole = Custom("Lead too", MembersDelete, MembersRead);
        var target = Membership(targetRole);

        var result = Active().Remove(Membership(actorRole), new MemberFacts(target, 1, RolesWith(actorRole, targetRole)));

        result.Value.ShouldBe(target);
    }

    [Fact]
    public void RemoveMember_AMemberWhoseRoleHoldsAPermissionTheActorDoesNot_IsAnEscalation()
    {
        var actorRole = Custom("Lead", MembersDelete, MembersRead, ProfileRead);

        var result = Active().Remove(Membership(actorRole), new MemberFacts(Membership(RoleOf("Admin")), 1, RolesWith(actorRole)));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleEscalation);
    }

    [Fact]
    public void RevokeInvitation_AnInvitationOfARoleTheActorHoldsExactly_CancelsIt()
    {
        var actorRole = Custom("Recruiter", InvitationsDelete, MembersRead);
        var invitedRole = Custom("Recruiter too", InvitationsDelete, MembersRead);
        var invitation = PendingTo("ada@example.com", invitedRole);

        var revoked = Active().Revoke(actorRole, invitation, RolesWith(actorRole, invitedRole));

        revoked.Value.ShouldBe(StateChange.Changed);
    }

    // I1: a cancelled invitation of a stronger role answers as it is, and an accepted one as not pending; neither gives a role any more.
    [Fact]
    public void RevokeInvitation_ACancelledInvitationOfAStrongerRole_ChangesNothing()
    {
        var actorRole = Custom("Recruiter", InvitationsDelete, MembersRead, ProfileRead);
        var invitation = PendingTo("ada@example.com", RoleOf("Admin"));
        invitation.Cancel();

        var revoked = Active().Revoke(actorRole, invitation, RolesWith(actorRole));

        revoked.Value.ShouldBe(StateChange.Unchanged);
    }

    [Fact]
    public void RevokeInvitation_AnAcceptedInvitationOfAStrongerRole_IsNotPending()
    {
        var actorRole = Custom("Recruiter", InvitationsDelete, MembersRead, ProfileRead);
        var invitation = PendingTo("ada@example.com", RoleOf("Admin"));
        invitation.Accept(Verified("ada@example.com"), Guid.NewGuid(), Now);

        var revoked = Active().Revoke(actorRole, invitation, RolesWith(actorRole));

        revoked.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationNotPending);
    }

    [Fact]
    public void RevokeInvitation_AnInvitationOfARoleWithAPermissionTheActorDoesNotHold_IsAnEscalationAndStaysPending()
    {
        var actorRole = Custom("Recruiter", InvitationsDelete, MembersRead, ProfileRead);
        var invitation = PendingTo("ada@example.com", RoleOf("Admin"));

        var revoked = Active().Revoke(actorRole, invitation, RolesWith(actorRole));

        revoked.Error.Code.ShouldBe(ControlPlaneErrorCodes.RoleEscalation);
        invitation.Status.ShouldBe(InvitationStatus.Pending);
    }

    private static Tenant Active() => Tenant.Create(Id, "Acme Ltd", "acme", PlanId, CreatedAt).Value;

    private static Tenant Suspended()
    {
        var tenant = Active();
        tenant.Suspend(Reason);
        return tenant;
    }

    private static Tenant Closed()
    {
        var tenant = Active();
        tenant.Close(Now, []);
        return tenant;
    }

    private static Tenant InStatus(string status)
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", PlanId, CreatedAt).Value;
        _ = status switch
        {
            "Active" => true,
            "Suspended" => tenant.Suspend(Reason).Value is StateChange.Changed,
            "Closed" => tenant.Close(Now, []).Value.Change is StateChange.Changed,
            "Purging" => tenant.Close(Now, []).Value.Change is StateChange.Changed && tenant.BeginPurge(Now) is StateChange.Changed,
            "Purged" => tenant.Close(Now, []).Value.Change is StateChange.Changed && tenant.BeginPurge(Now) is StateChange.Changed
                && tenant.Purge(Now) is StateChange.Changed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
        return tenant;
    }

    private static Role RoleOf(string role) => Roles.BuiltIn(Enum.Parse<BuiltInRole>(role));

    private static IReadOnlyList<EmailAddress> Verified(string email) => EmailAddress.AllOf([email]);

    private static Inviter MemberOf(string role) => Inviter.Member(InviterId, Language.Tr, RoleOf(role));

    private static NewInvitation Request(string email, Role role, Language? language = Language.En) =>
        new(Guid.NewGuid(), EmailAddress.Create(email), role, language, InvitationSecret.Generate(), Now, Lifetime);

    private static InvitationFacts Facts(int owners, bool member = false, int sent = 0, IReadOnlyList<Invitation>? pending = null) =>
        new(owners, member, sent, pending ?? []);

    private static Invitation PendingTo(string email, Role role) =>
        Invitation.Create(Guid.NewGuid(), Id, EmailAddress.Create(email), role, InviterId, Now, Lifetime, InvitationSecret.Generate(), Language.En);

    private static Membership Membership(string role) => new(Id, Guid.NewGuid(), RoleOf(role).Id, DateTimeOffset.UnixEpoch);

    private static Role Custom(string name, params string[] permissions) =>
        Role.CreateCustom(Guid.NewGuid(), Id, name, permissions.ToFrozenSet(StringComparer.Ordinal));

    // The tenant's built-in roles and these custom roles.
    private static TenantRoles RolesWith(params Role[] custom) =>
        new([.. Enum.GetValues<BuiltInRole>().Select(builtIn => Roles.BuiltIn(builtIn)), .. custom]);

    private static NewRole NewRole(string name, params string[] permissions) => new(Guid.NewGuid(), name, permissions);

    private static RoleFacts RoleFacts(Role? target = null, int customRoles = 0, bool nameTaken = false, bool inUse = false) =>
        new(target, customRoles, nameTaken, inUse ? new RoleUse(1, 0) : RoleUse.None);

    private static Membership Membership(Role role) => new(Id, Guid.NewGuid(), role.Id, DateTimeOffset.UnixEpoch);
}
