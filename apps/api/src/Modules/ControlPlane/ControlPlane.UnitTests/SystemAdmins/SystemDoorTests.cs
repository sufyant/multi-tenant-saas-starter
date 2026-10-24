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

    // A6a: a change of the system admin list needs a second factor verified less than the window ago, as Clerk counts it.
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void CheckReverification_ASecondFactorYoungerThanTheWindow_Passes(int minutes)
    {
        var checkedAge = SystemDoor.CheckReverification(TimeSpan.FromMinutes(minutes), TimeSpan.FromMinutes(10));

        checkedAge.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    public void CheckReverification_ASecondFactorAsOldAsTheWindowOrOlder_AsksForReverification(int minutes)
    {
        var checkedAge = SystemDoor.CheckReverification(TimeSpan.FromMinutes(minutes), TimeSpan.FromMinutes(10));

        checkedAge.Error.Code.ShouldBe(ControlPlaneErrorCodes.SystemAdminReverificationRequired);
    }

    [Fact]
    public void CheckReverification_NoSecondFactor_AsksForReverification()
    {
        var checkedAge = SystemDoor.CheckReverification(null, TimeSpan.FromMinutes(10));

        checkedAge.Error.Code.ShouldBe(ControlPlaneErrorCodes.SystemAdminReverificationRequired);
    }

    // docs/flows/system-admins.md: a person with a second factor and a verified address in the list becomes a system admin.
    [Fact]
    public void BecomeRootSystemAdmin_AListedEmailIsVerifiedWithASecondFactor_AnswersThatEmail()
    {
        var root = SystemDoor.RootEmailOf(Roots("Admin@Example.com", "boss@example.com"), secondFactorVerified: true, EmailAddress.AllOf(["other@example.com", "admin@example.com"]));

        root.ShouldBe(EmailAddress.Create("admin@example.com"));
    }

    [Fact]
    public void BecomeRootSystemAdmin_WithoutASecondFactor_AnswersNone()
    {
        var root = SystemDoor.RootEmailOf(Roots("admin@example.com"), secondFactorVerified: false, EmailAddress.AllOf(["admin@example.com"]));

        root.ShouldBeNull();
    }

    [Fact]
    public void BecomeRootSystemAdmin_NoListedEmailIsVerified_AnswersNone()
    {
        var root = SystemDoor.RootEmailOf(Roots("admin@example.com"), secondFactorVerified: true, EmailAddress.AllOf(["admin@example.org"]));

        root.ShouldBeNull();
    }

    // Root is read from the list, by the user's address in its one form; nothing stores it.
    [Theory]
    [InlineData("admin@example.com", true)]
    [InlineData("other@example.com", false)]
    [InlineData(null, false)]
    public void IsRoot_TheUsersEmailAndTheList_AnswersWhetherTheListNamesIt(string? email, bool isRoot)
    {
        var answer = SystemDoor.IsRoot(email, Roots("Admin@Example.com"));

        answer.ShouldBe(isRoot);
    }

    private static IReadOnlyList<EmailAddress> Roots(params string[] emails) => EmailAddress.AllOf(emails);
}
