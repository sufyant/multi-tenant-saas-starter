using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.UnitTests.Users;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SuspendUser_Active_IsSuspendedFromThatTime()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");

        user.Suspend(Now);

        user.SuspendedAt.ShouldBe(Now);
    }

    // Suspending sets a state: a repeat keeps the time of the first suspension.
    [Fact]
    public void SuspendUser_Twice_KeepsTheFirstTime()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");
        user.Suspend(Now);

        user.Suspend(Now.AddHours(1));

        user.SuspendedAt.ShouldBe(Now);
    }

    [Fact]
    public void ReactivateUser_Suspended_IsNoLongerSuspended()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");
        user.Suspend(Now);

        user.Reactivate();

        user.SuspendedAt.ShouldBeNull();
    }

    // A change is announced once, so Audit records it once (K3): only the first suspension changes the user.
    [Fact]
    public void SuspendUser_Twice_ChangesTheUserOnlyTheFirstTime()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");

        var first = user.Suspend(Now);
        var second = user.Suspend(Now.AddHours(1));

        (first, second).ShouldBe((true, false));
    }

    [Fact]
    public void ReactivateUser_NotSuspended_ChangesNothing()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");
        user.Suspend(Now);

        var first = user.Reactivate();
        var second = user.Reactivate();

        (first, second).ShouldBe((true, false));
    }

    [Fact]
    public void JoinUser_NamesWithSpaces_KeepsThemTrimmed()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "  Ada ", " Lovelace  ", Language.En);

        (user.Email, user.FirstName, user.LastName).ShouldBe(("ada@example.com", "Ada", "Lovelace"));
    }

    [Fact]
    public void RenameUser_AnotherName_IsAChange()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En);

        var changed = user.Rename("Ada", "King");

        changed.Value.ShouldBeTrue();
        (user.FirstName, user.LastName).ShouldBe(("Ada", "King"));
    }

    // Renaming sets a state: the same names, with spaces around them, change nothing, so Audit records nothing (K3).
    [Fact]
    public void RenameUser_SameNamesWithSpaces_IsNoChange()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En);

        var changed = user.Rename(" Ada ", "Lovelace ");

        changed.Value.ShouldBeFalse();
    }

    // The deletion erased the names; a new one would bring personal data back.
    [Fact]
    public void RenameUser_Deleted_IsRefusedAndStaysErased()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En);
        user.Delete(Now);

        var renamed = user.Rename("Ada", "King");

        renamed.Error.Code.Value.ShouldBe("user.deleted");
        (user.FirstName, user.LastName).ShouldBe((null, null));
    }

    [Fact]
    public void DeleteUser_Twice_ChangesTheUserOnlyTheFirstTime()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");

        var first = user.Delete(Now);
        var second = user.Delete(Now.AddHours(1));

        (first, second).ShouldBe((true, false));
    }

    // Decision 6 of step 20j: a new user speaks the language of the invitation they accept. The time zone is not set, so the web uses
    // the browser's.
    [Fact]
    public void JoinUser_ThroughATurkishInvitation_SpeaksTurkishWithNoTimeZone()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.Tr);

        (user.Language, user.TimeZone).ShouldBe((Language.Tr, null));
    }

    [Fact]
    public void ChangePreferences_NewValues_IsAChange()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En);

        var changed = user.ChangePreferences(Language.Ar, "Asia/Dubai");

        changed.Value.ShouldBeTrue();
        (user.Language, user.TimeZone).ShouldBe((Language.Ar, "Asia/Dubai"));
    }

    // Changing the preferences sets a state (I1): the values the user has change nothing, so Audit records nothing (K3).
    [Fact]
    public void ChangePreferences_TheValuesTheUserHas_IsNoChange()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.Tr);
        user.ChangePreferences(Language.Tr, "Europe/Istanbul");

        var changed = user.ChangePreferences(Language.Tr, "Europe/Istanbul");

        changed.Value.ShouldBeFalse();
    }

    // Null means "not set": the web uses the browser's time zone.
    [Fact]
    public void ChangePreferences_NoTimeZone_ClearsIt()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En);
        user.ChangePreferences(Language.En, "Europe/Dublin");

        var changed = user.ChangePreferences(Language.En, null);

        changed.Value.ShouldBeTrue();
        user.TimeZone.ShouldBeNull();
    }

    // A9: a deleted user is refused on every me endpoint, also when the deletion commits while the request waits for the user's row.
    [Fact]
    public void ChangePreferences_Deleted_IsForbiddenAndChangesNothing()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En);
        user.Delete(Now);

        var changed = user.ChangePreferences(Language.Tr, "Europe/Istanbul");

        (changed.Error.Type, changed.Error.Code.Value).ShouldBe((ErrorType.Forbidden, "user.deleted"));
        (user.Language, user.TimeZone).ShouldBe((Language.En, null));
    }

    // A time zone tells where a person lives, so the deletion erases it with the names (A8).
    [Fact]
    public void DeleteUser_WithATimeZone_ErasesIt()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En);
        user.ChangePreferences(Language.En, "Europe/London");

        user.Delete(Now);

        user.TimeZone.ShouldBeNull();
    }
}
