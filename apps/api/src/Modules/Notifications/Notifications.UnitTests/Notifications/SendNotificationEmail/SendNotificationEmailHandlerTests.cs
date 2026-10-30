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
// the event. A copy of the message sends one email.
public sealed class SendNotificationEmailHandlerTests
{
    private static readonly IOptions<NotificationLinkOptions> Links = Options.Create(
        new NotificationLinkOptions { WebUrl = new("https://app.example/"), PlatformUrl = new("https://platform.example/") });

    private static readonly Guid Ada = new("0199c000-0000-7000-8000-00000000ada0");

    private static readonly Dictionary<Guid, UserName> People = new() { [Ada] = new UserName("Ada Lovelace", Deleted: false) };

    private readonly RecordingEmailChannel _channel = new();

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
            + "We always send this email when you are removed from an organisation, so you know you no longer have access. It can’t be turned off.");
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

    // A type that is not required says which category's emails are on, by the category's name in the language.
    [Fact]
    public async Task SendNotificationEmail_ATypeOfACategory_NamesTheCategoryInTheFooter()
    {
        var values = new Dictionary<string, string> { ["tenantName"] = "Acme", ["actorName"] = Ada.ToString() };

        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(Guid.CreateVersion7(), NotificationCatalog.TenantSuspended, values, "ada@example.com", Language.En, People: People),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        _channel.Sent.ShouldHaveSingleItem().Text.ShouldEndWith(
            "You got this email because email notifications for “Platform” are on.");
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
