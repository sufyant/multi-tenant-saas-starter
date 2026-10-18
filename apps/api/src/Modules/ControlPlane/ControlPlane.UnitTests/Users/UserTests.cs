using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.UnitTests.Users;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid ActorId = new("0199a8f0-0000-7000-8000-000000000901");

    private static readonly Guid OtherAdminId = new("0199a8f0-0000-7000-8000-000000000902");

    // The user detail names the system admin who suspended the user.
    [Fact]
    public void SuspendUser_ByASystemAdmin_KeepsWhoSuspendedThem()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");

        user.Suspend(Now, ActorId);

        (user.SuspendedAt, user.SuspendedBy).ShouldBe((Now, ActorId));
    }

    [Fact]
    public void SuspendUser_TwiceByTwoSystemAdmins_KeepsTheFirst()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");
        user.Suspend(Now, ActorId);

        user.Suspend(Now.AddHours(1), OtherAdminId);

        (user.SuspendedAt, user.SuspendedBy).ShouldBe((Now, ActorId));
    }

    [Fact]
    public void ReactivateUser_Suspended_ForgetsWhoSuspendedThem()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");
        user.Suspend(Now, ActorId);

        user.Reactivate();

        user.SuspendedBy.ShouldBeNull();
    }

    // A person who joins through an invitation is created at that moment. The catalog adds and counts the membership of the tenant they
    // join, as it does for a known user (B8), so a new user starts with none.
    [Fact]
    public void JoinUser_NewPerson_IsCreatedAtTheMomentWithNoMembershipYet()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En, Now);

        (user.CreatedAt, user.MembershipCount).ShouldBe((Now, 0));
    }

    [Fact]
    public void SuspendUser_Active_IsSuspendedFromThatTime()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");

        user.Suspend(Now, ActorId);

        user.SuspendedAt.ShouldBe(Now);
    }

    // Suspending sets a state: a repeat keeps the time of the first suspension.
    [Fact]
    public void SuspendUser_Twice_KeepsTheFirstTime()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");
        user.Suspend(Now, ActorId);

        user.Suspend(Now.AddHours(1), ActorId);

        user.SuspendedAt.ShouldBe(Now);
    }

    [Fact]
    public void ReactivateUser_Suspended_IsNoLongerSuspended()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");
        user.Suspend(Now, ActorId);

        user.Reactivate();

        user.SuspendedAt.ShouldBeNull();
    }

    // A change is announced once, so Audit records it once (K3): only the first suspension changes the user.
    [Fact]
    public void SuspendUser_Twice_ChangesTheUserOnlyTheFirstTime()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");

        var first = user.Suspend(Now, ActorId);
        var second = user.Suspend(Now.AddHours(1), ActorId);

        (first, second).ShouldBe((StateChange.Changed, StateChange.Unchanged));
    }

    [Fact]
    public void ReactivateUser_NotSuspended_ChangesNothing()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");
        user.Suspend(Now, ActorId);

        var first = user.Reactivate();
        var second = user.Reactivate();

        (first, second).ShouldBe((StateChange.Changed, StateChange.Unchanged));
    }

    [Fact]
    public void JoinUser_NamesWithSpaces_KeepsThemTrimmed()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "  Ada ", " Lovelace  ", Language.En, Now);

        (user.Email, user.FirstName, user.LastName).ShouldBe(("ada@example.com", "Ada", "Lovelace"));
    }

    [Fact]
    public void RenameUser_AnotherName_IsAChange()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En, Now);

        var changed = user.Rename("Ada", "King");

        changed.Value.ShouldBe(StateChange.Changed);
        (user.FirstName, user.LastName).ShouldBe(("Ada", "King"));
    }

    // Renaming sets a state: the same names, with spaces around them, change nothing, so Audit records nothing (K3).
    [Fact]
    public void RenameUser_SameNamesWithSpaces_IsNoChange()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En, Now);

        var changed = user.Rename(" Ada ", "Lovelace ");

        changed.Value.ShouldBe(StateChange.Unchanged);
    }

    // The deletion erased the names; a new one would bring personal data back.
    [Fact]
    public void RenameUser_Deleted_IsRefusedAndStaysErased()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En, Now);
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

        (first, second).ShouldBe((StateChange.Changed, StateChange.Unchanged));
    }

    // Decision 6 of step 20j: a new user speaks the language of the invitation they accept. The time zone is not set, so the web uses
    // the browser's.
    [Fact]
    public void JoinUser_ThroughATurkishInvitation_SpeaksTurkishWithNoTimeZone()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.Tr, Now);

        (user.Language, user.TimeZone).ShouldBe((Language.Tr, null));
    }

    [Fact]
    public void ChangePreferences_NewValues_IsAChange()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En, Now);

        var changed = user.ChangePreferences(Language.Ar, "Asia/Dubai");

        changed.Value.ShouldBe(StateChange.Changed);
        (user.Language, user.TimeZone).ShouldBe((Language.Ar, "Asia/Dubai"));
    }

    // Changing the preferences sets a state (I1): the values the user has change nothing, so Audit records nothing (K3).
    [Fact]
    public void ChangePreferences_TheValuesTheUserHas_IsNoChange()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.Tr, Now);
        user.ChangePreferences(Language.Tr, "Europe/Istanbul");

        var changed = user.ChangePreferences(Language.Tr, "Europe/Istanbul");

        changed.Value.ShouldBe(StateChange.Unchanged);
    }

    // Null means "not set": the web uses the browser's time zone.
    [Fact]
    public void ChangePreferences_NoTimeZone_ClearsIt()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En, Now);
        user.ChangePreferences(Language.En, "Europe/Dublin");

        var changed = user.ChangePreferences(Language.En, null);

        changed.Value.ShouldBe(StateChange.Changed);
        user.TimeZone.ShouldBeNull();
    }

    // A9: a deleted user is refused on every me endpoint, also when the deletion commits while the request waits for the user's row.
    [Fact]
    public void ChangePreferences_Deleted_IsForbiddenAndChangesNothing()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En, Now);
        user.Delete(Now);

        var changed = user.ChangePreferences(Language.Tr, "Europe/Istanbul");

        (changed.Error.Type, changed.Error.Code.Value).ShouldBe((ErrorType.Forbidden, "user.deleted"));
        (user.Language, user.TimeZone).ShouldBe((Language.En, null));
    }

    // A time zone tells where a person lives, so the deletion erases it with the names (A8).
    [Fact]
    public void DeleteUser_WithATimeZone_ErasesIt()
    {
        var user = User.Join(Guid.CreateVersion7(), "user_1", "ada@example.com", "Ada", "Lovelace", Language.En, Now);
        user.ChangePreferences(Language.En, "Europe/London");

        user.Delete(Now);

        user.TimeZone.ShouldBeNull();
    }

    // B9: a deletion outranks a suspension, and a query reads the status by the same rule as the user does.
    [Fact]
    public void DeleteUser_Suspended_IsDeletedToTheUserAndToAQuery()
    {
        var user = new User(Guid.CreateVersion7(), "user_1");
        user.Suspend(Now, ActorId);

        user.Delete(Now);

        user.Status.ShouldBe(UserStatus.Deleted);
        User.InStatus(UserStatus.Deleted).Compile()(user).ShouldBeTrue();
        User.InStatus(UserStatus.Suspended).Compile()(user).ShouldBeFalse();
    }
}
