using ControlPlane.Domain;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.UnitTests.Tenants;

// The rules of the Tenant aggregate (M1): its creation, status and name, its Owner invitation, and the Owner rules of a
// role change and a removal (K1). Each check is pure up to the change, so a rejection changes nothing.
public sealed class TenantTests
{
    private const string Reason = "Unpaid invoices";

    private const int DailyLimit = 50;

    private static readonly Guid Id = new("0199a8f0-0000-7000-8000-000000000001");

    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan ReopenPeriod = TimeSpan.FromDays(30);

    private static readonly Guid InviterId = new("0199a8f0-0000-7000-8000-000000000002");

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private static readonly Inviter Platform = Inviter.Platform(InviterId, Language.Tr);

    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    public static TheoryData<string> InvalidNames => ["", "   ", new string('n', 101)];

    // Settings › General shows when the tenant was created.
    [Fact]
    public void CreateTenant_Valid_KeepsWhenItWasCreated()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt);

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
        var tenant = Tenant.Create(Id, "Acme Ltd", slug, CreatedAt);

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
        var tenant = Tenant.Create(Id, "Acme Ltd", slug, CreatedAt);

        tenant.IsSuccess.ShouldBeFalse();
        tenant.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantSlugInvalid);
    }

    [Fact]
    public void CreateTenant_NameWithSpacesAround_IsKeptWithoutThem()
    {
        var tenant = Tenant.Create(Id, "  Acme Ltd ", "acme", CreatedAt);

        tenant.Value.Name.ShouldBe("Acme Ltd");
    }

    [Fact]
    public void CreateTenant_NameOfOneHundredCharacters_IsAccepted()
    {
        var name = new string('n', 100);

        var tenant = Tenant.Create(Id, name, "acme", CreatedAt);

        tenant.Value.Name.ShouldBe(name);
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void CreateTenant_InvalidName_IsRejected(string name)
    {
        var tenant = Tenant.Create(Id, name, "acme", CreatedAt);

        tenant.IsSuccess.ShouldBeFalse();
        tenant.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNameInvalid);
    }

    [Fact]
    public void CreateTenant_New_IsProvisioning()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt).Value;

        tenant.Status.ShouldBe(TenantStatus.Provisioning);
    }

    [Fact]
    public void ActivateTenant_WhenProvisioning_IsActive()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt).Value;

        tenant.Activate();

        tenant.Status.ShouldBe(TenantStatus.Active);
    }

    // A tenant's creation ends active or cancelled, never both.
    [Fact]
    public void ActivateTenant_Cancelled_StaysCancelled()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt).Value;
        tenant.Cancel("identity_provider_failed");

        var activated = tenant.Activate();

        activated.ShouldBe(StateChange.Unchanged);
        tenant.Status.ShouldBe(TenantStatus.Cancelled);
    }

    [Fact]
    public void CancelTenant_Provisioning_IsCancelledWithItsReason()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt).Value;

        var cancelled = tenant.Cancel("identity_provider_failed");

        cancelled.ShouldBe(StateChange.Changed);
        tenant.Status.ShouldBe(TenantStatus.Cancelled);
        tenant.CancellationReason.ShouldBe("identity_provider_failed");
    }

    // A compensation may arrive twice; the first reason stays.
    [Fact]
    public void CancelTenant_AlreadyCancelled_KeepsTheFirstReason()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt).Value;
        tenant.Cancel("identity_provider_failed");

        var cancelled = tenant.Cancel("registration_timed_out");

        cancelled.ShouldBe(StateChange.Unchanged);
        tenant.CancellationReason.ShouldBe("identity_provider_failed");
    }

    // Activation is the pivot: an active tenant is not cancelled.
    [Fact]
    public void CancelTenant_Active_StaysActive()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt).Value;
        tenant.Activate();

        var cancelled = tenant.Cancel("activation_failed");

        cancelled.ShouldBe(StateChange.Unchanged);
        tenant.Status.ShouldBe(TenantStatus.Active);
        tenant.CancellationReason.ShouldBeNull();
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

    // A tenant whose creation has not passed its pivot, or never will, is neither suspended nor reactivated.
    [Fact]
    public void SuspendOrReactivateTenant_Provisioning_IsRejected()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt).Value;

        var suspended = tenant.Suspend(Reason);
        var reactivated = tenant.Reactivate();

        suspended.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
        reactivated.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
        tenant.Status.ShouldBe(TenantStatus.Provisioning);
    }

    [Fact]
    public void SuspendOrReactivateTenant_Cancelled_IsRejected()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt).Value;
        tenant.Cancel("activation_failed");

        var suspended = tenant.Suspend(Reason);
        var reactivated = tenant.Reactivate();

        suspended.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
        reactivated.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
        tenant.Status.ShouldBe(TenantStatus.Cancelled);
    }

    // A suspended tenant is past the pivot: a late compensation does not cancel it.
    [Fact]
    public void CancelTenant_Suspended_StaysSuspended()
    {
        var tenant = Active();
        tenant.Suspend(Reason);

        var cancelled = tenant.Cancel("activation_failed");

        cancelled.ShouldBe(StateChange.Unchanged);
        tenant.Status.ShouldBe(TenantStatus.Suspended);
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
    [InlineData("Provisioning")]
    [InlineData("Cancelled")]
    [InlineData("Closed")]
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

        var closed = tenant.Close(Now);

        closed.Value.ShouldBe(StateChange.Changed);
        tenant.Status.ShouldBe(TenantStatus.Closed);
        tenant.ClosedAt.ShouldBe(Now);
    }

    // The reason stays until the tenant is reopened.
    [Fact]
    public void CloseTenant_Suspended_KeepsTheReason()
    {
        var tenant = Active();
        tenant.Suspend(Reason);

        tenant.Close(Now);

        tenant.SuspensionReason.ShouldBe(Reason);
    }

    // Closing sets a state: a repeat changes nothing, the moment of the closure included.
    [Fact]
    public void CloseTenant_Closed_ChangesNothingAndKeepsTheFirstMoment()
    {
        var tenant = Closed();

        var closed = tenant.Close(Now.AddDays(1));

        closed.Value.ShouldBe(StateChange.Unchanged);
        tenant.ClosedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("Provisioning")]
    [InlineData("Cancelled")]
    public void CloseTenant_ProvisioningOrCancelled_IsRejected(string status)
    {
        var tenant = InStatus(status);

        var closed = tenant.Close(Now);

        closed.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
        tenant.ClosedAt.ShouldBeNull();
    }

    // A closed tenant is past the pivot: a late compensation does not cancel it.
    [Fact]
    public void CancelTenant_Closed_StaysClosed()
    {
        var tenant = Closed();

        var cancelled = tenant.Cancel("activation_failed");

        cancelled.ShouldBe(StateChange.Unchanged);
        tenant.Status.ShouldBe(TenantStatus.Closed);
    }

    [Fact]
    public void ReopenTenant_ClosedWithinThePeriod_IsActiveAndForgetsTheClosureAndTheReason()
    {
        var tenant = Active();
        tenant.Suspend(Reason);
        tenant.Close(Now);

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
    [InlineData("Provisioning")]
    [InlineData("Cancelled")]
    [InlineData("Suspended")]
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

    [Fact]
    public void ReadReopenableUntil_AClosedTenant_IsItsClosurePlusThePeriod()
    {
        var tenant = Closed();

        tenant.ReopenableUntil(ReopenPeriod).ShouldBe(Now + ReopenPeriod);
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    [InlineData("Provisioning")]
    public void ReadReopenableUntil_ATenantThatIsNotClosed_IsNone(string status)
    {
        var tenant = InStatus(status);

        tenant.ReopenableUntil(ReopenPeriod).ShouldBeNull();
    }

    [Fact]
    public void InviteOwner_AnActiveTenantWithoutAnOwner_CreatesAPendingOwnerInvitation()
    {
        var invited = Active().Invite(Platform, Request("ada@example.com", BuiltInRoles.Owner), Facts(owners: 0), DailyLimit);

        invited.Value.Invitation.Email.ShouldBe("ada@example.com");
        invited.Value.Invitation.RoleId.ShouldBe(BuiltInRoles.Owner.Id);
        invited.Value.Invitation.TenantId.ShouldBe(Id);
        invited.Value.Invitation.InvitedBy.ShouldBe(InviterId);
        invited.Value.Invitation.Status.ShouldBe(InvitationStatus.Pending);
        invited.Value.Replaced.ShouldBeEmpty();
    }

    [Fact]
    public void InviteOwner_ATenantWithAnOwner_IsAConflict()
    {
        var invited = Active().Invite(Platform, Request("ada@example.com", BuiltInRoles.Owner), Facts(owners: 1), DailyLimit);

        invited.Error.Type.ShouldBe(ErrorType.Conflict);
        invited.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantHasOwner);
    }

    [Theory]
    [InlineData("Provisioning")]
    [InlineData("Suspended")]
    [InlineData("Cancelled")]
    [InlineData("Closed")]
    public void InviteOwner_ATenantThatIsNotActive_IsAConflict(string status)
    {
        var invited = InStatus(status).Invite(Platform, Request("ada@example.com", BuiltInRoles.Owner), Facts(owners: 0), DailyLimit);

        invited.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
    }

    // Nobody else invites into a tenant without an Owner, so every pending invitation is the platform's, and the new one replaces it,
    // also to another address.
    [Fact]
    public void InviteOwner_PendingInvitationsToOtherAddresses_CancelsEachOne()
    {
        var first = PendingTo("grace@example.com", BuiltInRoles.Owner);
        var second = PendingTo("ada@example.com", BuiltInRoles.Owner);

        var invited = Active().Invite(Platform, Request("ada@example.com", BuiltInRoles.Owner), Facts(owners: 0, pending: [first, second]), DailyLimit);

        invited.Value.Replaced.ShouldBe([first, second]);
        first.Status.ShouldBe(InvitationStatus.Cancelled);
        second.Status.ShouldBe(InvitationStatus.Cancelled);
    }

    // The daily limit is a member's; the platform's Owner invitation and a member's address are no rules of the platform.
    [Fact]
    public void InviteOwner_PastTheDailyLimit_IsAllowed()
    {
        var invited = Active().Invite(Platform, Request("ada@example.com", BuiltInRoles.Owner), Facts(owners: 0, sent: DailyLimit), DailyLimit);

        invited.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void InviteOwner_WithoutALanguage_TakesTheSystemAdmins()
    {
        var invited = Active().Invite(Platform, Request("ada@example.com", BuiltInRoles.Owner, language: null), Facts(owners: 0), DailyLimit);

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
        var pending = PendingTo("ada@example.com", BuiltInRoles.Member);

        var invited = Active().Invite(
            MemberOf("Admin"), Request("ada@example.com", BuiltInRoles.Owner), Facts(owners: 1, member: true, pending: [pending]), DailyLimit);

        invited.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationOwnerRequiresOwner);
        pending.Status.ShouldBe(InvitationStatus.Pending);
    }

    // The address comes before the limit.
    [Fact]
    public void InviteMember_AMembersAddressAtTheDailyLimit_IsAConflict()
    {
        var invited = Active().Invite(MemberOf("Owner"), Request("ada@example.com", BuiltInRoles.Member), Facts(owners: 1, member: true, sent: DailyLimit), DailyLimit);

        invited.Error.Type.ShouldBe(ErrorType.Conflict);
        invited.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberExists);
    }

    [Fact]
    public void InviteMember_AtTheDailyLimit_IsTooManyRequestsAndCancelsNothing()
    {
        var pending = PendingTo("ada@example.com", BuiltInRoles.Member);

        var invited = Active().Invite(MemberOf("Owner"), Request("ada@example.com", BuiltInRoles.Member), Facts(owners: 1, sent: DailyLimit, pending: [pending]), DailyLimit);

        invited.Error.Type.ShouldBe(ErrorType.TooManyRequests);
        invited.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationDailyLimitReached);
        pending.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public void InviteMember_OneBelowTheDailyLimit_IsAllowed()
    {
        var invited = Active().Invite(MemberOf("Owner"), Request("ada@example.com", BuiltInRoles.Member), Facts(owners: 1, sent: DailyLimit - 1), DailyLimit);

        invited.IsSuccess.ShouldBeTrue();
    }

    // K2: the new invitation replaces a pending one to the same address, and only that one.
    [Fact]
    public void InviteMember_APendingInvitationToTheAddress_CancelsItAndKeepsTheOthers()
    {
        var same = PendingTo("ada@example.com", BuiltInRoles.Member);
        var other = PendingTo("grace@example.com", BuiltInRoles.Member);

        var invited = Active().Invite(MemberOf("Owner"), Request("ada@example.com", BuiltInRoles.Admin), Facts(owners: 1, pending: [same, other]), DailyLimit);

        invited.Value.Replaced.ShouldBe([same]);
        same.Status.ShouldBe(InvitationStatus.Cancelled);
        other.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public void InviteMember_WithALanguage_KeepsIt()
    {
        var invited = Active().Invite(MemberOf("Owner"), Request("ada@example.com", BuiltInRoles.Member, Language.Ar), Facts(owners: 1), DailyLimit);

        invited.Value.Invitation.Language.ShouldBe(Language.Ar);
    }

    [Fact]
    public void InviteMember_WithoutALanguage_TakesTheInviters()
    {
        var invited = Active().Invite(MemberOf("Owner"), Request("ada@example.com", BuiltInRoles.Member, language: null), Facts(owners: 1), DailyLimit);

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

        var result = Tenant.ChangeMemberRole(Membership(actor), changed, RoleOf(role), owners: 2);

        result.Value.ShouldBe(StateChange.Changed);
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

        var result = Tenant.ChangeMemberRole(Membership("Admin"), changed, RoleOf(role), owners: 2);

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

        var result = Tenant.ChangeMemberRole(Membership("Owner"), changed, RoleOf(role), owners: 1);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberLastOwner);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        changed.RoleId.ShouldBe(BuiltInRoles.Owner.Id);
    }

    // K1: an Owner steps down when another Owner exists, and cannot as the last one.
    [Fact]
    public void ChangeMemberRole_AnOwnerStepsDownWithAnotherOwner_ChangesIt()
    {
        var self = Membership("Owner");

        var result = Tenant.ChangeMemberRole(self, self, BuiltInRoles.Admin, owners: 2);

        result.Value.ShouldBe(StateChange.Changed);
        self.RoleId.ShouldBe(BuiltInRoles.Admin.Id);
    }

    [Fact]
    public void ChangeMemberRole_TheLastOwnerStepsDown_IsAConflict()
    {
        var self = Membership("Owner");

        var result = Tenant.ChangeMemberRole(self, self, BuiltInRoles.Admin, owners: 1);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberLastOwner);
        self.RoleId.ShouldBe(BuiltInRoles.Owner.Id);
    }

    // K1: an Admin may become a Member, and cannot make themselves an Owner.
    [Fact]
    public void ChangeMemberRole_AnAdminBecomesAMember_ChangesIt()
    {
        var self = Membership("Admin");

        var result = Tenant.ChangeMemberRole(self, self, BuiltInRoles.Member, owners: 1);

        result.Value.ShouldBe(StateChange.Changed);
    }

    [Fact]
    public void ChangeMemberRole_AnAdminMakesThemselvesAnOwner_IsForbidden()
    {
        var self = Membership("Admin");

        var result = Tenant.ChangeMemberRole(self, self, BuiltInRoles.Owner, owners: 1);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberOwnerRequiresOwner);
        self.RoleId.ShouldBe(BuiltInRoles.Admin.Id);
    }

    // K1 and I1: the role the member already has changes nothing. The last Owner keeps the Owner role, which is no loss.
    [Theory]
    [InlineData("Owner", 1)]
    [InlineData("Admin", 1)]
    [InlineData("Member", 1)]
    public void ChangeMemberRole_TheRoleTheMemberHas_ChangesNothing(string role, int owners)
    {
        var result = Tenant.ChangeMemberRole(Membership("Owner"), Membership(role), RoleOf(role), owners);

        result.Value.ShouldBe(StateChange.Unchanged);
    }

    // Section 6, "Member management": an Admin who acts on an Owner is refused before the Owner count is looked at.
    [Fact]
    public void ChangeMemberRole_AnAdminOnTheLastOwner_IsForbiddenFirst()
    {
        var result = Tenant.ChangeMemberRole(Membership("Admin"), Membership("Owner"), BuiltInRoles.Member, owners: 1);

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
        var result = Tenant.CheckRemoval(Membership(actor), Membership(target), owners: 2);

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    public void RemoveMember_Themselves_IsForbidden(string role)
    {
        var self = Membership(role);

        var result = Tenant.CheckRemoval(self, self, owners: 2);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberCannotRemoveSelf);
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public void RemoveMember_AnAdminRemovesAnOwner_IsForbidden()
    {
        var result = Tenant.CheckRemoval(Membership("Admin"), Membership("Owner"), owners: 2);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberOwnerRequiresOwner);
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    // Only the count of Owners decides: a tenant whose Owner count is one keeps that Owner, whoever acts.
    [Fact]
    public void RemoveMember_TheLastOwner_IsAConflict()
    {
        var result = Tenant.CheckRemoval(Membership("Owner"), Membership("Owner"), owners: 1);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberLastOwner);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    // Section 6, "Member management": removing yourself is refused before any other check, also for the last Owner.
    [Fact]
    public void RemoveMember_TheLastOwnerThemselves_IsForbiddenFirst()
    {
        var self = Membership("Owner");

        var result = Tenant.CheckRemoval(self, self, owners: 1);

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.MemberCannotRemoveSelf);
    }

    private static Tenant Active()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt).Value;
        tenant.Activate();
        return tenant;
    }

    private static Tenant Closed()
    {
        var tenant = Active();
        tenant.Close(Now);
        return tenant;
    }

    private static Tenant InStatus(string status)
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt).Value;
        _ = status switch
        {
            "Provisioning" => true,
            "Cancelled" => tenant.Cancel("activation_failed") is StateChange.Changed,
            "Active" => tenant.Activate() is StateChange.Changed,
            "Suspended" => tenant.Activate() is StateChange.Changed && tenant.Suspend(Reason).Value is StateChange.Changed,
            "Closed" => tenant.Activate() is StateChange.Changed && tenant.Close(Now).Value is StateChange.Changed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
        return tenant;
    }

    private static Role RoleOf(string role) => BuiltInRoles.All.Single(builtIn => builtIn.BuiltIn.ToString() == role);

    private static Inviter MemberOf(string role) => Inviter.Member(InviterId, Language.Tr, RoleOf(role));

    private static NewInvitation Request(string email, Role role, Language? language = Language.En) =>
        new(Guid.NewGuid(), EmailAddress.Create(email), role, language, InvitationSecret.Generate(), Now, Lifetime);

    private static InvitationFacts Facts(int owners, bool member = false, int sent = 0, IReadOnlyList<Invitation>? pending = null) =>
        new(owners, member, sent, pending ?? []);

    private static Invitation PendingTo(string email, Role role) =>
        Invitation.Create(Guid.NewGuid(), Id, EmailAddress.Create(email), role, InviterId, CreatedAt, Lifetime, InvitationSecret.Generate(), Language.En);

    private static Membership Membership(string role) => new(Id, Guid.NewGuid(), RoleOf(role).Id, DateTimeOffset.UnixEpoch);
}
