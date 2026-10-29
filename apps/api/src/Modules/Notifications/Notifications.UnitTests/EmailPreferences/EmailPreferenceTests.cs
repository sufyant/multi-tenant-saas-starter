using Notifications.Domain;
using Notifications.Domain.EmailPreferences;
using SharedKernel;

namespace Notifications.UnitTests.EmailPreferences;

// U4, U5: the main switch, the switch of each category, the defaults of the catalog, and the required type that ignores them.
public sealed class EmailPreferenceTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void SendsEmail_NothingChosen_FollowsTheCatalogsDefaultWhichIsOffToday()
    {
        var preference = EmailPreference.DefaultFor(UserId);

        preference.SendsEmail(NotificationCatalog.Of(NotificationCatalog.TenantPlanChanged)).ShouldBeFalse();
    }

    [Fact]
    public void SendsEmail_TheCategoryOn_SendsIt()
    {
        var preference = EmailPreference.DefaultFor(UserId);
        preference.Change(enabled: true, new Dictionary<string, bool> { [NotificationCatalog.PlanCategory] = true });

        preference.SendsEmail(NotificationCatalog.Of(NotificationCatalog.TenantPlanChanged)).ShouldBeTrue();
    }

    [Fact]
    public void SendsEmail_TheMainSwitchOffAndTheCategoryOn_SendsNone()
    {
        var preference = EmailPreference.DefaultFor(UserId);
        preference.Change(enabled: false, new Dictionary<string, bool> { [NotificationCatalog.PlanCategory] = true });

        preference.SendsEmail(NotificationCatalog.Of(NotificationCatalog.TenantPlanChanged)).ShouldBeFalse();
    }

    [Fact]
    public void SendsEmail_ARequiredTypeWithTheMainSwitchOff_SendsItAnyway()
    {
        var preference = EmailPreference.DefaultFor(UserId);
        preference.Change(enabled: false, new Dictionary<string, bool> { [NotificationCatalog.AccessCategory] = false });

        preference.SendsEmail(NotificationCatalog.Of(NotificationCatalog.MemberRemoved)).ShouldBeTrue();
    }

    [Fact]
    public void Change_BackToEveryDefault_KeepsNothingToStore()
    {
        var preference = EmailPreference.DefaultFor(UserId);
        preference.Change(enabled: false, new Dictionary<string, bool> { [NotificationCatalog.MembersCategory] = true });

        var changed = preference.Change(enabled: true, new Dictionary<string, bool> { [NotificationCatalog.MembersCategory] = false });

        (changed, preference.IsDefault).ShouldBe((StateChange.Changed, true));
    }

    [Fact]
    public void Change_TheSameChoices_IsUnchanged()
    {
        var preference = EmailPreference.DefaultFor(UserId);
        preference.Change(enabled: true, new Dictionary<string, bool> { [NotificationCatalog.PlanCategory] = true });

        var changed = preference.Change(enabled: true, new Dictionary<string, bool> { [NotificationCatalog.PlanCategory] = true });

        changed.ShouldBe(StateChange.Unchanged);
    }

    [Fact]
    public void Change_ACategoryTheCatalogDoesNotKnow_IsAMistakeOfOurs()
    {
        var preference = EmailPreference.DefaultFor(UserId);

        Action change = () => preference.Change(enabled: true, new Dictionary<string, bool> { ["billing"] = true });

        change.ShouldThrow<InvalidOperationException>().Message.ShouldContain("billing");
    }
}
