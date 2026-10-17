using ControlPlane.Domain;
using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.UnitTests.Tenants;

public class TenantTests
{
    private static readonly Guid Id = new("0199a8f0-0000-7000-8000-000000000001");

    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan ReopenPeriod = TimeSpan.FromDays(30);

    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private const string Reason = "Unpaid invoices";

    // Settings › General shows when the organisation was created.
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

    public static TheoryData<string> InvalidNames => ["", "   ", new string('n', 101)];

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

        activated.ShouldBeFalse();
        tenant.Status.ShouldBe(TenantStatus.Cancelled);
    }

    [Fact]
    public void CancelTenant_Provisioning_IsCancelledWithItsReason()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt).Value;

        var cancelled = tenant.Cancel("identity_provider_failed");

        cancelled.ShouldBeTrue();
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

        cancelled.ShouldBeFalse();
        tenant.CancellationReason.ShouldBe("identity_provider_failed");
    }

    // Activation is the pivot: an active tenant is not cancelled.
    [Fact]
    public void CancelTenant_Active_StaysActive()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme", CreatedAt).Value;
        tenant.Activate();

        var cancelled = tenant.Cancel("activation_failed");

        cancelled.ShouldBeFalse();
        tenant.Status.ShouldBe(TenantStatus.Active);
        tenant.CancellationReason.ShouldBeNull();
    }

    [Fact]
    public void SuspendTenant_Active_IsSuspended()
    {
        var tenant = Active();

        var suspended = tenant.Suspend(Reason);

        suspended.Value.ShouldBeTrue();
        tenant.Status.ShouldBe(TenantStatus.Suspended);
    }

    // Suspending sets a state: a repeat changes nothing and reports no change, so nothing is announced twice.
    [Fact]
    public void SuspendTenant_Suspended_StaysSuspendedWithoutAChange()
    {
        var tenant = Active();
        tenant.Suspend(Reason);

        var suspended = tenant.Suspend(Reason);

        suspended.Value.ShouldBeFalse();
        tenant.Status.ShouldBe(TenantStatus.Suspended);
    }

    [Fact]
    public void ReactivateTenant_Suspended_IsActive()
    {
        var tenant = Active();
        tenant.Suspend(Reason);

        var reactivated = tenant.Reactivate();

        reactivated.Value.ShouldBeTrue();
        tenant.Status.ShouldBe(TenantStatus.Active);
    }

    [Fact]
    public void ReactivateTenant_Active_StaysActiveWithoutAChange()
    {
        var tenant = Active();

        var reactivated = tenant.Reactivate();

        reactivated.Value.ShouldBeFalse();
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

        cancelled.ShouldBeFalse();
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

        renamed.Value.ShouldBeTrue();
        tenant.Name.ShouldBe("Globex Corp");
        tenant.Slug.ShouldBe("acme");
    }

    [Fact]
    public void RenameTenant_Suspended_IsRenamed()
    {
        var tenant = Active();
        tenant.Suspend(Reason);

        var renamed = tenant.Rename("Globex Corp");

        renamed.Value.ShouldBeTrue();
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

        renamed.Value.ShouldBeFalse();
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

        closed.Value.ShouldBeTrue();
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

        closed.Value.ShouldBeFalse();
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

        cancelled.ShouldBeFalse();
        tenant.Status.ShouldBe(TenantStatus.Closed);
    }

    [Fact]
    public void ReopenTenant_ClosedWithinThePeriod_IsActiveAndForgetsTheClosureAndTheReason()
    {
        var tenant = Active();
        tenant.Suspend(Reason);
        tenant.Close(Now);

        var reopened = tenant.Reopen(Now + ReopenPeriod - TimeSpan.FromSeconds(1), ReopenPeriod);

        reopened.Value.ShouldBeTrue();
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

        reopened.Value.ShouldBeFalse();
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

    // R12: the summary says what the tenant's rows say. The latest Owner invitation stays only while the tenant has no Owner: its address
    // is a person's (the maintainer's decision in step 20k).
    [Fact]
    public void SummarizeTenant_WithoutAnOwner_KeepsTheCountsAndTheLatestOwnerInvitation()
    {
        var tenant = Active();
        var invitation = OwnerInvitation();

        tenant.Summarize(new TenantRows(0, 0, null, invitation));

        (tenant.MemberCount, tenant.OwnerCount, tenant.FirstOwnerId).ShouldBe((0, 0, null));
        (tenant.OwnerInvitationEmail, tenant.OwnerInvitationExpiresAt, tenant.OwnerInvitationStatus)
            .ShouldBe(("ada@example.com", Now.AddDays(7), InvitationStatus.Pending));
    }

    [Fact]
    public void SummarizeTenant_AnOwnerJoined_KeepsTheFirstOwnerAndClearsTheOwnerInvitation()
    {
        var tenant = Active();
        tenant.Summarize(new TenantRows(0, 0, null, OwnerInvitation()));
        var owner = Guid.CreateVersion7();

        tenant.Summarize(new TenantRows(3, 2, owner, OwnerInvitation()));

        (tenant.MemberCount, tenant.OwnerCount, tenant.FirstOwnerId).ShouldBe((3, 2, owner));
        (tenant.OwnerInvitationEmail, tenant.OwnerInvitationExpiresAt, tenant.OwnerInvitationStatus).ShouldBe((null, null, null));
        tenant.OwnerInvitationStateAt(Now).ShouldBeNull();
    }

    // "Expired" is read from the expiry at the moment it is asked, never stored.
    [Theory]
    [InlineData(0, "Pending")]
    [InlineData(7, "Expired")]
    [InlineData(8, "Expired")]
    public void ReadOwnerInvitationState_APendingInvitation_IsExpiredFromItsExpiryOn(int days, string state)
    {
        var tenant = Active();
        tenant.Summarize(new TenantRows(0, 0, null, OwnerInvitation()));

        tenant.OwnerInvitationStateAt(Now.AddDays(days)).ShouldBe(Enum.Parse<OwnerInvitationState>(state));
    }

    [Fact]
    public void ReadOwnerInvitationState_ACancelledInvitation_IsCancelled()
    {
        var tenant = Active();
        var invitation = OwnerInvitation();
        invitation.Cancel();
        tenant.Summarize(new TenantRows(0, 0, null, invitation));

        tenant.OwnerInvitationStateAt(Now).ShouldBe(OwnerInvitationState.Cancelled);
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
    public void InviteOwner_AnActiveTenantWithoutAnOwner_IsAllowed()
    {
        var allowed = Active().CheckOwnerInvitation(owners: 0);

        allowed.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void InviteOwner_ATenantWithAnOwner_IsAConflict()
    {
        var allowed = Active().CheckOwnerInvitation(owners: 1);

        allowed.Error.Type.ShouldBe(ErrorType.Conflict);
        allowed.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantHasOwner);
    }

    [Theory]
    [InlineData("Provisioning")]
    [InlineData("Suspended")]
    [InlineData("Cancelled")]
    [InlineData("Closed")]
    public void InviteOwner_ATenantThatIsNotActive_IsAConflict(string status)
    {
        var allowed = InStatus(status).CheckOwnerInvitation(owners: 0);

        allowed.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotActive);
    }

    private static Invitation OwnerInvitation() =>
        Invitation.Create(Guid.CreateVersion7(), Id, "ada@example.com", BuiltInRoles.Owner, Guid.CreateVersion7(), Now, TimeSpan.FromDays(7), "secret", Language.En);

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
            "Cancelled" => tenant.Cancel("activation_failed"),
            "Active" => tenant.Activate(),
            "Suspended" => tenant.Activate() && tenant.Suspend(Reason).Value,
            "Closed" => tenant.Activate() && tenant.Close(Now).Value,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
        return tenant;
    }
}
