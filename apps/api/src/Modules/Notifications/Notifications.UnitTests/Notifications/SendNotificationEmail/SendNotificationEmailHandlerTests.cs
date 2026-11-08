using ControlPlane.Contracts.Users;
using Microsoft.Extensions.Options;
using Notifications.Application.Email;
using Notifications.Application.Notifications;
using Notifications.Application.Notifications.SendNotificationEmail;
using Notifications.Domain;
using SharedKernel;

namespace Notifications.UnitTests.Notifications.SendNotificationEmail;

// U2, U4: the generic notification email carries the title and the body that the bell shows, in the recipient's language, and one button
// to the web app, or to the platform app for the platform's notifications. It names the people with the names it carries, from the moment of
// the event. A copy of the message sends one email. The footer of its type says only why the email came: nobody chooses the channels (U4, U5).
public sealed class SendNotificationEmailHandlerTests
{
    private static readonly IOptions<NotificationLinkOptions> Links = Options.Create(
        new NotificationLinkOptions { WebUrl = new("https://app.example/"), PlatformUrl = new("https://platform.example/") });

    private static readonly Guid Ada = new("0199c000-0000-7000-8000-00000000ada0");

    private static readonly Dictionary<Guid, UserName> People = new() { [Ada] = new UserName("Ada Lovelace", Deleted: false) };

    private readonly RecordingEmailChannel _channel = new();

    // The footer of each status type of the platform, in each language.
    public static TheoryData<string, Language, string> PlatformFooters => new()
    {
        { NotificationCatalog.TenantSuspended, Language.En, "You received this because you are a system admin." },
        { NotificationCatalog.TenantReactivated, Language.En, "You received this because you are a system admin." },
        { NotificationCatalog.TenantClosed, Language.En, "You received this because you are a system admin." },
        { NotificationCatalog.TenantReopened, Language.En, "You received this because you are a system admin." },
        { NotificationCatalog.TenantPurged, Language.En, "You received this because you are a system admin." },
        { NotificationCatalog.TenantSuspended, Language.Tr, "Bu e-postayı, sistem yöneticisi olduğunuz için aldınız." },
        { NotificationCatalog.TenantPurged, Language.Tr, "Bu e-postayı, sistem yöneticisi olduğunuz için aldınız." },
        { NotificationCatalog.TenantSuspended, Language.Ar, "وصلتك هذه الرسالة لأنك مسؤول النظام." },
        { NotificationCatalog.TenantPurged, Language.Ar, "وصلتك هذه الرسالة لأنك مسؤول النظام." },
    };

    [Fact]
    public async Task SendNotificationEmail_ARemovedMember_EmailsTheTitleAndTheBodyWithAButtonToTheWebApp()
    {
        var notificationId = Guid.CreateVersion7();

        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(notificationId, NotificationCatalog.MemberRemoved, Removal("Acme"), "ada@example.com", Language.En, People: People),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.To, email.Subject, email.IdempotencyKey).ShouldBe(("ada@example.com", "You were removed from Acme", $"notification/{notificationId}"));
        email.Text.ShouldBe(
            "Starter\n\nYou were removed from Acme\n\nAda Lovelace removed you from Acme. You can no longer open it or see its notifications.\n\nOpen Starter:\nhttps://app.example/\n\n"
            + "You received this because your access to an organisation changed.");
        email.Html.ShouldContain("href=\"https://app.example/\"");
    }

    [Fact]
    public async Task SendNotificationEmail_APlatformNotificationInTurkish_LeadsToThePlatformInTheLanguage()
    {
        var values = new Dictionary<string, string> { ["tenantName"] = "Acme", ["actorName"] = Ada.ToString() };

        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(Guid.CreateVersion7(), NotificationCatalog.TenantSuspended, values, "ada@example.com", Language.Tr, People: People),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.Subject, email.Html.Contains("href=\"https://platform.example/\"", StringComparison.Ordinal)).ShouldBe(("Acme askıya alındı", true));
    }

    // A notification of the link kind AccountSecurity leads to Account and security, which the web opens without an organisation.
    [Fact]
    public async Task SendNotificationEmail_ADataExportReady_LeadsToAccountAndSecurityOfTheWebApp()
    {
        var values = new Dictionary<string, string> { ["expiresAt"] = "2026-11-14T12:00:00.0000000+00:00" };

        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(Guid.CreateVersion7(), NotificationCatalog.UserDataExportReady, values, "ada@example.com", Language.En),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.Text.Contains("Open Starter:\nhttps://app.example/settings/account\n", StringComparison.Ordinal),
            email.Html.Contains("href=\"https://app.example/settings/account\"", StringComparison.Ordinal)).ShouldBe((true, true));
    }

    // A value is HTML-encoded in the HTML part, as in every email (section 7, "Email").
    [Fact]
    public async Task SendNotificationEmail_ATenantNameWithHtml_EscapesItInTheHtml()
    {
        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(Guid.CreateVersion7(), NotificationCatalog.MemberRemoved, Removal("<b>Acme</b>"), "ada@example.com", Language.En, People: People),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        _channel.Sent.ShouldHaveSingleItem().Html.ShouldContain("&lt;b&gt;Acme&lt;/b&gt;");
    }

    // U5: every email says why it came, in its type's own footer, and none links to a settings page. A system admin hears of an
    // organisation's status because they are a system admin, as the platform's email boards write it (14.03 to 14.05).
    [Theory]
    [MemberData(nameof(PlatformFooters))]
    public async Task SendNotificationEmail_APlatformNotification_SaysItCameToASystemAdminWithoutALinkToSettings(
        string type, Language language, string footer)
    {
        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(Guid.CreateVersion7(), type, StatusChange("Acme"), "ada@example.com", language, People: People),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.Text.EndsWith($"https://platform.example/\n\n{footer}", StringComparison.Ordinal),
            email.Html.Contains(footer, StringComparison.Ordinal),
            email.Html.Contains("settings", StringComparison.Ordinal)).ShouldBe((true, true, false));
    }

    // A deleted organisation's name and address stay in the platform's list: the email says "address", the word of the screens, never
    // "slug" (14.05).
    [Theory]
    [InlineData(Language.En, "The platform finished deleting Acme and its data. Its name and address stay in the list.")]
    [InlineData(Language.Tr, "Platform, Acme kuruluşunu ve verilerini silmeyi tamamladı. Adı ve adresi listede kalır.")]
    [InlineData(Language.Ar, "أنهت المنصة حذف Acme وبياناتها. يبقى اسمها وعنوانها في القائمة.")]
    public async Task SendNotificationEmail_APurgedTenant_SaysItsNameAndAddressStayInTheList(Language language, string body)
    {
        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(Guid.CreateVersion7(), NotificationCatalog.TenantPurged, StatusChange("Acme"), "ada@example.com", language, People: People),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[2].ShouldBe(body);
    }

    // U5: an Owner hears of the plan because they are an Owner.
    [Fact]
    public async Task SendNotificationEmail_APlanChange_SaysItCameToAnOwner()
    {
        var values = new Dictionary<string, string> { ["tenantName"] = "Acme", ["oldPlanName"] = "Free", ["newPlanName"] = "Pro" };

        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(Guid.CreateVersion7(), NotificationCatalog.TenantPlanChanged, values, "ada@example.com", Language.En),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        _channel.Sent.ShouldHaveSingleItem().Text.ShouldEndWith("https://app.example/\n\nYou received this because you are an Owner of this organisation.");
    }

    // U5: a changed role's footer names the organisation where it changed, as the email boards write it (13.05).
    [Theory]
    [InlineData(Language.En, "You received this because your role in Acme changed.")]
    [InlineData(Language.Tr, "Bu e-postayı, Acme kuruluşundaki rolünüz değiştiği için aldınız.")]
    [InlineData(Language.Ar, "وصلتك هذه الرسالة لأن دورك في Acme تغيّر.")]
    public async Task SendNotificationEmail_AChangedRole_NamesTheOrganisationInTheFooter(Language language, string footer)
    {
        var values = new Dictionary<string, string>
        {
            ["tenantName"] = "Acme",
            ["actorName"] = Ada.ToString(),
            ["oldRoleName"] = "member",
            ["oldRoleNameBuiltIn"] = "true",
            ["newRoleName"] = "admin",
            ["newRoleNameBuiltIn"] = "true",
        };

        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(Guid.CreateVersion7(), NotificationCatalog.MemberRoleChanged, values, "ada@example.com", language, People: People),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.Text.Split("\n\n")[^1], email.Html.Contains(footer, StringComparison.Ordinal)).ShouldBe((footer, true));
    }

    // The email writes a date in the recipient's time zone: noon in UTC on 29 November is 30 November in Auckland.
    [Fact]
    public async Task SendNotificationEmail_AClosedTenant_WritesTheDeletionDateInTheRecipientsTimeZone()
    {
        var values = new Dictionary<string, string>
        {
            ["tenantName"] = "Acme",
            ["actorName"] = Ada.ToString(),
            ["reopenableUntil"] = "2026-11-29T12:00:00.0000000+00:00",
        };

        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(Guid.CreateVersion7(), NotificationCatalog.TenantClosed, values, "ada@example.com", Language.En, "Pacific/Auckland", People),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        _channel.Sent.ShouldHaveSingleItem().Text.ShouldContain("Ada Lovelace closed Acme. It will be deleted on 30 Nov 2026 unless someone reopens it.");
    }

    // A message of the version before carries no names, so the email names the person "Deleted user": it never shows an id.
    [Fact]
    public async Task SendNotificationEmail_AMessageWithoutNames_NamesThePersonDeletedUser()
    {
        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(Guid.CreateVersion7(), NotificationCatalog.MemberRemoved, Removal("Acme"), "ada@example.com", Language.En),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        _channel.Sent.ShouldHaveSingleItem().Text.ShouldContain("Deleted user removed you from Acme.");
    }

    private static Dictionary<string, string> Removal(string tenantName) => new() { ["tenantName"] = tenantName, ["actorName"] = Ada.ToString() };

    // The values of every status type of the platform: a type writes the ones its texts name.
    private static Dictionary<string, string> StatusChange(string tenantName) => new()
    {
        ["tenantName"] = tenantName,
        ["actorName"] = Ada.ToString(),
        ["reopenableUntil"] = "2026-12-01T09:00:00.0000000+00:00",
    };

    private sealed class RecordingEmailChannel : IEmailChannel
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
        {
            Sent.Add(email);
            return Task.CompletedTask;
        }
    }
}
