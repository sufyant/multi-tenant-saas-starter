using Microsoft.Extensions.Options;
using Notifications.Application.Email;
using Notifications.Application.Notifications;
using Notifications.Application.Notifications.SendNotificationEmail;
using Notifications.Domain;
using SharedKernel;

namespace Notifications.UnitTests.Notifications.SendNotificationEmail;

// U2, U4: the generic notification email carries the title and the body that the bell shows, in the recipient's language, and one button
// to the web app, or to the platform app for the platform's notifications. A copy of the message sends one email.
public sealed class SendNotificationEmailHandlerTests
{
    private static readonly IOptions<NotificationLinkOptions> Links = Options.Create(
        new NotificationLinkOptions { WebUrl = new("https://app.example/"), PlatformUrl = new("https://platform.example/") });

    private readonly RecordingEmailChannel _channel = new();

    [Fact]
    public async Task SendNotificationEmail_ARemovedMember_EmailsTheTitleAndTheBodyWithAButtonToTheWebApp()
    {
        var notificationId = Guid.CreateVersion7();

        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(notificationId, NotificationCatalog.MemberRemoved, new Dictionary<string, string> { ["tenantName"] = "Acme" }, "ada@example.com", Language.En),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.To, email.Subject, email.IdempotencyKey).ShouldBe(("ada@example.com", "You were removed from Acme", $"notification/{notificationId}"));
        email.Text.ShouldBe(
            "Starter\n\nYou were removed from Acme\n\nYou no longer have access to Acme.\n\nOpen Starter:\nhttps://app.example/\n\n"
            + "You received this because of your email preferences for notifications. You change them in your preferences.");
        email.Html.ShouldContain("href=\"https://app.example/\"");
    }

    [Fact]
    public async Task SendNotificationEmail_APlatformNotificationInTurkish_LeadsToThePlatformInTheLanguage()
    {
        var values = new Dictionary<string, string> { ["tenantName"] = "Acme", ["status"] = NotificationCatalog.StatusSuspended };

        await SendNotificationEmailHandler.HandleAsync(
            new SendNotificationEmailCommand(Guid.CreateVersion7(), NotificationCatalog.TenantStatusChanged, values, "ada@example.com", Language.Tr),
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
            new SendNotificationEmailCommand(Guid.CreateVersion7(), NotificationCatalog.MemberRemoved, new Dictionary<string, string> { ["tenantName"] = "<b>Acme</b>" }, "ada@example.com", Language.En),
            _channel,
            Links,
            TestContext.Current.CancellationToken);

        _channel.Sent.ShouldHaveSingleItem().Html.ShouldContain("&lt;b&gt;Acme&lt;/b&gt;");
    }

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
