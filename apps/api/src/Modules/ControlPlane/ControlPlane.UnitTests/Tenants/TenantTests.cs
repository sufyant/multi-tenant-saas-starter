using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.UnitTests.Tenants;

public class TenantTests
{
    private static readonly Guid Id = new("0199a8f0-0000-7000-8000-000000000001");

    [Theory]
    [InlineData("acme")]
    [InlineData("acme-corp")]
    [InlineData("a1b")]
    [InlineData("tenant-2024-eu")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk")]
    public void CreateTenant_UrlSafeSlug_IsCreatedWithThatSlug(string slug)
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", slug);

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
        var tenant = Tenant.Create(Id, "Acme Ltd", slug);

        tenant.IsSuccess.ShouldBeFalse();
        tenant.Error.Code.ShouldBe(ErrorCode.TenantSlugInvalid);
    }

    [Fact]
    public void CreateTenant_NameWithSpacesAround_IsKeptWithoutThem()
    {
        var tenant = Tenant.Create(Id, "  Acme Ltd ", "acme");

        tenant.Value.Name.ShouldBe("Acme Ltd");
    }

    [Fact]
    public void CreateTenant_NameOfOneHundredCharacters_IsAccepted()
    {
        var name = new string('n', 100);

        var tenant = Tenant.Create(Id, name, "acme");

        tenant.Value.Name.ShouldBe(name);
    }

    public static TheoryData<string> InvalidNames => ["", "   ", new string('n', 101)];

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void CreateTenant_InvalidName_IsRejected(string name)
    {
        var tenant = Tenant.Create(Id, name, "acme");

        tenant.IsSuccess.ShouldBeFalse();
        tenant.Error.Code.ShouldBe(ErrorCode.TenantNameInvalid);
    }

    [Fact]
    public void CreateTenant_New_IsProvisioning()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme").Value;

        tenant.Status.ShouldBe(TenantStatus.Provisioning);
    }

    [Fact]
    public void ActivateTenant_WhenProvisioning_IsActive()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme").Value;

        tenant.Activate();

        tenant.Status.ShouldBe(TenantStatus.Active);
    }

    // A tenant's creation ends active or cancelled, never both.
    [Fact]
    public void ActivateTenant_Cancelled_StaysCancelled()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme").Value;
        tenant.Cancel("identity_provider_failed");

        var activated = tenant.Activate();

        activated.ShouldBeFalse();
        tenant.Status.ShouldBe(TenantStatus.Cancelled);
    }

    [Fact]
    public void CancelTenant_Provisioning_IsCancelledWithItsReason()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme").Value;

        var cancelled = tenant.Cancel("identity_provider_failed");

        cancelled.ShouldBeTrue();
        tenant.Status.ShouldBe(TenantStatus.Cancelled);
        tenant.CancellationReason.ShouldBe("identity_provider_failed");
    }

    // A compensation may arrive twice; the first reason stays.
    [Fact]
    public void CancelTenant_AlreadyCancelled_KeepsTheFirstReason()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme").Value;
        tenant.Cancel("identity_provider_failed");

        var cancelled = tenant.Cancel("registration_timed_out");

        cancelled.ShouldBeFalse();
        tenant.CancellationReason.ShouldBe("identity_provider_failed");
    }

    // Activation is the pivot: an active tenant is not cancelled.
    [Fact]
    public void CancelTenant_Active_StaysActive()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme").Value;
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

        var suspended = tenant.Suspend();

        suspended.Value.ShouldBeTrue();
        tenant.Status.ShouldBe(TenantStatus.Suspended);
    }

    // Suspending sets a state: a repeat changes nothing and reports no change, so nothing is announced twice.
    [Fact]
    public void SuspendTenant_Suspended_StaysSuspendedWithoutAChange()
    {
        var tenant = Active();
        tenant.Suspend();

        var suspended = tenant.Suspend();

        suspended.Value.ShouldBeFalse();
        tenant.Status.ShouldBe(TenantStatus.Suspended);
    }

    [Fact]
    public void ReactivateTenant_Suspended_IsActive()
    {
        var tenant = Active();
        tenant.Suspend();

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
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme").Value;

        var suspended = tenant.Suspend();
        var reactivated = tenant.Reactivate();

        suspended.Error.Code.ShouldBe(ErrorCode.TenantNotActive);
        reactivated.Error.Code.ShouldBe(ErrorCode.TenantNotActive);
        tenant.Status.ShouldBe(TenantStatus.Provisioning);
    }

    [Fact]
    public void SuspendOrReactivateTenant_Cancelled_IsRejected()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme").Value;
        tenant.Cancel("activation_failed");

        var suspended = tenant.Suspend();
        var reactivated = tenant.Reactivate();

        suspended.Error.Code.ShouldBe(ErrorCode.TenantNotActive);
        reactivated.Error.Code.ShouldBe(ErrorCode.TenantNotActive);
        tenant.Status.ShouldBe(TenantStatus.Cancelled);
    }

    // A suspended tenant is past the pivot: a late compensation does not cancel it.
    [Fact]
    public void CancelTenant_Suspended_StaysSuspended()
    {
        var tenant = Active();
        tenant.Suspend();

        var cancelled = tenant.Cancel("activation_failed");

        cancelled.ShouldBeFalse();
        tenant.Status.ShouldBe(TenantStatus.Suspended);
    }

    private static Tenant Active()
    {
        var tenant = Tenant.Create(Id, "Acme Ltd", "acme").Value;
        tenant.Activate();
        return tenant;
    }
}
