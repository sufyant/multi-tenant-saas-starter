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
    public void CopyProfile_SameEmailAndNameAtALaterTime_IsNoChange()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");
        user.CopyProfile("ada@example.com", "Ada", "Lovelace", Now);

        var changed = user.CopyProfile("ada@example.com", "Ada", "Lovelace", Now.AddHours(1));

        changed.ShouldBeFalse();
        user.ProfileUpdatedAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void CopyProfile_AnotherName_IsAChange()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");
        user.CopyProfile("ada@example.com", "Ada", "Lovelace", Now);

        var changed = user.CopyProfile("ada@example.com", "Ada", "King", Now.AddHours(1));

        changed.ShouldBeTrue();
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
