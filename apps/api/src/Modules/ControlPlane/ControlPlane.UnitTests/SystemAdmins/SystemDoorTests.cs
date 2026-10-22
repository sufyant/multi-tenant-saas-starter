using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain;
using ControlPlane.Domain.SystemAdmins;

namespace ControlPlane.UnitTests.SystemAdmins;

// A6: the system door needs a system admin with a second factor verified in the session.
public sealed class SystemDoorTests
{
    [Fact]
    public void EnterSystemDoor_SystemAdminWithASecondFactor_HoldsThePlatformPool()
    {
        var permissions = SystemDoor.PermissionsFor(isSystemAdmin: true, secondFactorVerified: true);

        permissions.ShouldBe(PermissionPools.PlatformPool);
    }

    [Fact]
    public void EnterSystemDoor_SystemAdminWithoutASecondFactor_IsRefused()
    {
        var permissions = SystemDoor.PermissionsFor(isSystemAdmin: true, secondFactorVerified: false);

        permissions.ShouldBeNull();
    }

    [Fact]
    public void EnterSystemDoor_UserWhoIsNotASystemAdmin_IsRefused()
    {
        var permissions = SystemDoor.PermissionsFor(isSystemAdmin: false, secondFactorVerified: true);

        permissions.ShouldBeNull();
    }

    // Section 6: while the system admin list is empty, the person with the configured email address verified and a second factor becomes
    // the first system admin.
    [Fact]
    public void BecomeFirstSystemAdmin_TheConfiguredEmailIsVerifiedWithASecondFactor_Qualifies()
    {
        var qualifies = SystemDoor.IsFirstSystemAdmin(EmailAddress.Create("Admin@Example.com"), secondFactorVerified: true, EmailAddress.AllOf(["other@example.com", "admin@example.com"]));

        qualifies.ShouldBeTrue();
    }

    [Fact]
    public void BecomeFirstSystemAdmin_WithoutASecondFactor_DoesNotQualify()
    {
        var qualifies = SystemDoor.IsFirstSystemAdmin(EmailAddress.Create("admin@example.com"), secondFactorVerified: false, EmailAddress.AllOf(["admin@example.com"]));

        qualifies.ShouldBeFalse();
    }

    [Fact]
    public void BecomeFirstSystemAdmin_AnotherEmailIsVerified_DoesNotQualify()
    {
        var qualifies = SystemDoor.IsFirstSystemAdmin(EmailAddress.Create("admin@example.com"), secondFactorVerified: true, EmailAddress.AllOf(["admin@example.org"]));

        qualifies.ShouldBeFalse();
    }
}
