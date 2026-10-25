using ControlPlane.Application.Invitations;

namespace ControlPlane.UnitTests.Invitations;

// The invitation's email names who invited: a person by the names they have at that moment, or the platform, whose email names the
// brand. A system admin invites as the platform, and so does a user without names, so no email says that nobody invited.
public sealed class InvitingUserTests
{
    [Fact]
    public void NameForEmail_APerson_IsTheirFirstAndLastName() =>
        new InvitingUser(false, "Ayşe", "Yılmaz", null).NameForEmail.ShouldBe("Ayşe Yılmaz");

    [Fact]
    public void NameForEmail_AUserWithOnlyAFirstName_IsThatName() =>
        new InvitingUser(false, "Ayşe", null, null).NameForEmail.ShouldBe("Ayşe");

    [Fact]
    public void NameForEmail_ASystemAdmin_IsThePlatform() =>
        new InvitingUser(true, "Ayşe", "Yılmaz", "Europe/Istanbul").NameForEmail.ShouldBeNull();

    [Fact]
    public void NameForEmail_AUserWithoutNames_IsThePlatform() =>
        new InvitingUser(false, null, " ", null).NameForEmail.ShouldBeNull();
}
