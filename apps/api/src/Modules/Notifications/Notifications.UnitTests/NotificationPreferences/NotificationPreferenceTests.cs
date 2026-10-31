using Notifications.Domain;
using Notifications.Domain.NotificationPreferences;
using Notifications.Domain.Notifications;
using SharedKernel;

namespace Notifications.UnitTests.NotificationPreferences;

// U4, U5: a channel goes when the category is required, or when the channel's main switch is on and the person's choice, or without one
// the catalog's default, is on. Each combination is checked for one category, tenant_plans, on both channels; the required category,
// account_security, against every choice.
public sealed class NotificationPreferenceTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();

    private static readonly NotificationKind PlanChanged = NotificationCatalog.Of(NotificationCatalog.TenantPlanChanged);

    private static readonly NotificationKind Removed = NotificationCatalog.Of(NotificationCatalog.MemberRemoved);

    // A channel, its main switch, the person's choice of the category in it (none: the default), and whether the channel goes.
    public static TheoryData<NotificationChannel, bool, bool?, bool> CombinationsOfOneCategory => new()
    {
        { NotificationChannel.Web, true, null, true },
        { NotificationChannel.Web, true, true, true },
        { NotificationChannel.Web, true, false, false },
        { NotificationChannel.Web, false, null, false },
        { NotificationChannel.Web, false, true, false },
        { NotificationChannel.Email, true, null, false },
        { NotificationChannel.Email, true, true, true },
        { NotificationChannel.Email, true, false, false },
        { NotificationChannel.Email, false, null, false },
        { NotificationChannel.Email, false, true, false },
    };

    [Fact]
    public void Sends_NothingChosen_FollowsTheCatalogsDefaultsTheWebOnAndTheEmailOff()
    {
        var preference = NotificationPreference.DefaultFor(UserId);

        (preference.Sends(PlanChanged, NotificationChannel.Web), preference.Sends(PlanChanged, NotificationChannel.Email)).ShouldBe((true, false));
    }

    [Theory]
    [MemberData(nameof(CombinationsOfOneCategory))]
    public void Sends_OneCategory_GoesWhenTheMainSwitchAndTheChoiceOrTheDefaultAreOn(NotificationChannel channel, bool all, bool? chosen, bool expected)
    {
        var preference = NotificationPreference.DefaultFor(UserId);
        preference.Change(
            new Dictionary<NotificationChannel, bool> { [channel] = all },
            [.. new bool?[] { chosen }.OfType<bool>().Select(on => new NotificationChoice(NotificationCatalog.PlanCategory, channel, on))]);

        preference.Sends(PlanChanged, channel).ShouldBe(expected);
    }

    [Fact]
    public void Sends_TheWebsMainSwitchOffAndNoChoice_SendsNoBellButTheEmailStillFollowsItsOwnChannel()
    {
        var preference = NotificationPreference.DefaultFor(UserId);
        preference.Change(
            new Dictionary<NotificationChannel, bool> { [NotificationChannel.Web] = false },
            [new(NotificationCatalog.PlanCategory, NotificationChannel.Email, true)]);

        (preference.Sends(PlanChanged, NotificationChannel.Web), preference.Sends(PlanChanged, NotificationChannel.Email)).ShouldBe((false, true));
    }

    // The required category stores no choice of its own (Change_ARequiredCategory_IsAMistakeOfOurs), so every other choice is tried.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Sends_TheRequiredCategoryAgainstEveryMainSwitchAndChoice_GoesOnEveryChannel(bool on)
    {
        var preference = NotificationPreference.DefaultFor(UserId);
        preference.Change(
            new Dictionary<NotificationChannel, bool> { [NotificationChannel.Web] = on, [NotificationChannel.Email] = on },
            [.. NotificationCatalog.Categories.Where(category => category != NotificationCatalog.AccountSecurityCategory).SelectMany(category => Enum.GetValues<NotificationChannel>().Select(channel => new NotificationChoice(category, channel, on)))]);

        (preference.Sends(Removed, NotificationChannel.Web), preference.Sends(Removed, NotificationChannel.Email)).ShouldBe((true, true));
    }

    [Fact]
    public void IsOn_TheRequiredCategory_IsOnInEveryChannel()
    {
        var preference = NotificationPreference.DefaultFor(UserId);

        Enum.GetValues<NotificationChannel>().Select(channel => preference.IsOn(NotificationCatalog.AccountSecurityCategory, channel)).ShouldAllBe(on => on);
    }

    [Fact]
    public void Change_ARequiredCategory_IsAMistakeOfOurs()
    {
        var preference = NotificationPreference.DefaultFor(UserId);

        Action change = () => preference.Change(
            new Dictionary<NotificationChannel, bool>(), [new(NotificationCatalog.AccountSecurityCategory, NotificationChannel.Email, false)]);

        change.ShouldThrow<InvalidOperationException>().Message.ShouldContain("account_security");
    }

    [Fact]
    public void Change_ACategoryTheCatalogDoesNotKnow_IsAMistakeOfOurs()
    {
        var preference = NotificationPreference.DefaultFor(UserId);

        Action change = () => preference.Change(new Dictionary<NotificationChannel, bool>(), [new("billing", NotificationChannel.Email, true)]);

        change.ShouldThrow<InvalidOperationException>().Message.ShouldContain("billing");
    }

    [Fact]
    public void Change_BackToEveryDefault_KeepsNothingToStore()
    {
        var preference = NotificationPreference.DefaultFor(UserId);
        preference.Change(
            new Dictionary<NotificationChannel, bool> { [NotificationChannel.Email] = false },
            [new(NotificationCatalog.MembersCategory, NotificationChannel.Web, false)]);

        var changed = preference.Change(
            new Dictionary<NotificationChannel, bool> { [NotificationChannel.Email] = true },
            [new(NotificationCatalog.MembersCategory, NotificationChannel.Web, true)]);

        (changed, preference.IsDefault).ShouldBe((StateChange.Changed, true));
    }

    [Fact]
    public void Change_TheSameChoices_IsUnchanged()
    {
        var preference = NotificationPreference.DefaultFor(UserId);
        preference.Change(new Dictionary<NotificationChannel, bool>(), [new(NotificationCatalog.PlanCategory, NotificationChannel.Email, true)]);

        var changed = preference.Change(new Dictionary<NotificationChannel, bool>(), [new(NotificationCatalog.PlanCategory, NotificationChannel.Email, true)]);

        changed.ShouldBe(StateChange.Unchanged);
    }

    [Fact]
    public void Change_AChoiceLeftOut_KeepsItsValue()
    {
        var preference = NotificationPreference.DefaultFor(UserId);
        preference.Change(new Dictionary<NotificationChannel, bool>(), [new(NotificationCatalog.PlanCategory, NotificationChannel.Email, true)]);

        preference.Change(new Dictionary<NotificationChannel, bool>(), [new(NotificationCatalog.PlanCategory, NotificationChannel.Web, false)]);

        (preference.IsOn(NotificationCatalog.PlanCategory, NotificationChannel.Web), preference.IsOn(NotificationCatalog.PlanCategory, NotificationChannel.Email))
            .ShouldBe((false, true));
    }
}
