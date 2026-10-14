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
}
