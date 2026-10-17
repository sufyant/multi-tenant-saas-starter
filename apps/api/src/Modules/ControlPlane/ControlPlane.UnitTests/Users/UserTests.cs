using ControlPlane.Domain.Users;

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
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "  Ada ", " Lovelace  ");

        (user.Email, user.FirstName, user.LastName).ShouldBe(("ada@example.com", "Ada", "Lovelace"));
    }

    [Fact]
    public void RenameUser_AnotherName_IsAChange()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace");

        var changed = user.Rename("Ada", "King");

        changed.Value.ShouldBeTrue();
        (user.FirstName, user.LastName).ShouldBe(("Ada", "King"));
    }

    // Renaming sets a state: the same names, with spaces around them, change nothing, so Audit records nothing (K3).
    [Fact]
    public void RenameUser_SameNamesWithSpaces_IsNoChange()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace");

        var changed = user.Rename(" Ada ", "Lovelace ");

        changed.Value.ShouldBeFalse();
    }

    // The deletion erased the names; a new one would bring personal data back.
    [Fact]
    public void RenameUser_Deleted_IsRefusedAndStaysErased()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace");
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
}
