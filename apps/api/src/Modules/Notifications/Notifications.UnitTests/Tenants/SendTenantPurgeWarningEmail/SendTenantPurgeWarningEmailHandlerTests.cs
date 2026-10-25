using ControlPlane.Contracts.Tenants;
using Notifications.Application.Email;
using Notifications.Application.Tenants.SendTenantPurgeWarningEmail;
using SharedKernel;

namespace Notifications.UnitTests.Tenants.SendTenantPurgeWarningEmail;

// The Notifications module warns each Owner of a closed tenant before its purge, in the Owner's own language: the tenant is closed and
// will be deleted with its data at a moment, and the brand's support contact can reopen it before then (docs/flows/tenant-purge.md). It
// hears TenantPurgeWarningReadyEvent, which only it hears, so the email's retries may be scheduled (W5). The email is the generated
// deletion warning of packages/emails, in HTML and in plain text. The moment is in the Owner's own time zone, named with its offset as the
// UI names it; without a zone, or with one the image does not know, it is in UTC (section 7). It says no number of days.
public sealed class SendTenantPurgeWarningEmailHandlerTests
{
    private static readonly Guid TenantId = new("0199a8f0-0000-7000-8000-000000000801");

    private static readonly Guid UserId = new("0199a8f0-0000-7000-8000-000000000802");

    private static readonly DateTimeOffset PurgesAt = new(2026, 11, 22, 9, 30, 0, TimeSpan.Zero);

    private readonly RecordingEmailChannel _channel = new();

    public static TheoryData<Language, string, string> Texts => new()
    {
        {
            Language.En,
            "Acme Ltd will be deleted on 22 Nov 2026",
            "Starter\n\nAcme Ltd is closed.\n\nIt will be deleted with all its data on 22 Nov 2026 at 09:30 UTC.\n\n"
            + "Contact us to reopen it before that date.\nmailto:support@starter.example\n\n"
            + "You received this because you are an Owner of Acme Ltd."
        },
        {
            Language.Tr,
            "Acme Ltd 22 Kas 2026 tarihinde silinecek",
            "Starter\n\nAcme Ltd kapatıldı.\n\n22 Kas 2026, saat 09:30 UTC itibarıyla tüm verileriyle birlikte silinecek.\n\n"
            + "Bu tarihten önce yeniden açmak için bize ulaşın.\nmailto:support@starter.example\n\n"
            + "Bu e-postayı, Acme Ltd kuruluşunun Sahibi olduğunuz için aldınız."
        },
        {
            Language.Ar,
            "ستُحذف Acme Ltd في 22 نوفمبر 2026",
            "Starter\n\nأُغلقت Acme Ltd.\n\nستُحذف مع جميع بياناتها في 22 نوفمبر 2026 الساعة 09:30 بالتوقيت العالمي (UTC).\n\n"
            + "تواصل معنا لإعادة فتحها قبل هذا التاريخ.\nmailto:support@starter.example\n\n"
            + "وصلتك هذه الرسالة لأنك مالك في Acme Ltd."
        },
    };

    public static TheoryData<Language> Languages => [.. Enum.GetValues<Language>()];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendTenantPurgeWarningEmail_Asked_EmailsTheOwnersAddress()
    {
        await SendAsync(Ready(Language.En));

        _channel.Sent.ShouldHaveSingleItem().To.ShouldBe("ada@acme.test");
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public async Task SendTenantPurgeWarningEmail_InTheOwnersLanguage_WritesTheTenantAndTheMomentOfThePurge(Language language, string subject, string text)
    {
        await SendAsync(Ready(language));

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.Subject, email.Text).ShouldBe((subject, text));
    }

    // Every generated warning in every language has exactly the placeholders the handler fills; a missing or an extra one fails here.
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task SendTenantPurgeWarningEmail_EveryLanguage_FillsEveryPlaceholder(Language language)
    {
        await SendAsync(Ready(language));

        var email = _channel.Sent.ShouldHaveSingleItem();
        email.Html.ShouldStartWith("<!DOCTYPE html");
        $"{email.Subject}{email.Html}{email.Text}".ShouldNotContain("{{");
    }

    // "Contact us" links to the brand's support contact (packages/brand).
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task SendTenantPurgeWarningEmail_EveryLanguage_LinksContactUsToTheSupportContact(Language language)
    {
        await SendAsync(Ready(language));

        _channel.Sent.ShouldHaveSingleItem().Html.ShouldContain("href=\"mailto:support@starter.example\"");
    }

    // The moment in the Owner's zone, with the zone's city and its offset at that moment (F18 in the web app's architecture). The date is the
    // zone's, so Honolulu's purge comes the day before, and so does the day of the subject.
    [Theory]
    [InlineData("Europe/Istanbul", Language.En, "It will be deleted with all its data on 22 Nov 2026 at 12:30 Istanbul (UTC+3).")]
    [InlineData("Asia/Kolkata", Language.En, "It will be deleted with all its data on 22 Nov 2026 at 15:00 Kolkata (UTC+5:30).")]
    [InlineData("America/Argentina/Buenos_Aires", Language.En, "It will be deleted with all its data on 22 Nov 2026 at 06:30 Buenos Aires (UTC-3).")]
    [InlineData("Pacific/Honolulu", Language.En, "It will be deleted with all its data on 21 Nov 2026 at 23:30 Honolulu (UTC-10).")]
    [InlineData("Europe/Istanbul", Language.Tr, "22 Kas 2026, saat 12:30 Istanbul (UTC+3) itibarıyla tüm verileriyle birlikte silinecek.")]
    [InlineData("Europe/Istanbul", Language.Ar, "ستُحذف مع جميع بياناتها في 22 نوفمبر 2026 الساعة 12:30 بتوقيت Istanbul (UTC+3).")]
    public async Task SendTenantPurgeWarningEmail_AnOwnerWithATimeZone_WritesTheMomentInItWithTheZoneAndItsOffset(
        string timeZone, Language language, string body)
    {
        await SendAsync(Ready(language) with { TimeZone = timeZone });

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[2].ShouldBe(body);
    }

    [Fact]
    public async Task SendTenantPurgeWarningEmail_AnOwnerWhosZoneIsADayBehind_WritesTheZonesDayInTheSubject()
    {
        await SendAsync(Ready(Language.En) with { TimeZone = "Pacific/Honolulu" });

        _channel.Sent.ShouldHaveSingleItem().Subject.ShouldBe("Acme Ltd will be deleted on 21 Nov 2026");
    }

    // A zone the image does not know, as an old name that a later release of the time zone data drops, writes the moment in UTC.
    [Fact]
    public async Task SendTenantPurgeWarningEmail_AnOwnerWithAnUnknownTimeZone_WritesTheMomentInUtc()
    {
        await SendAsync(Ready(Language.En) with { TimeZone = "Mars/Olympus_Mons" });

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[2].ShouldBe("It will be deleted with all its data on 22 Nov 2026 at 09:30 UTC.");
    }

    // The tenant's name is data, never markup.
    [Fact]
    public async Task SendTenantPurgeWarningEmail_ATenantNameWithHtml_EscapesItInTheHtml()
    {
        await SendAsync(Ready(Language.En) with { TenantName = "<b>Acme</b> & co" });

        var email = _channel.Sent.ShouldHaveSingleItem();
        email.Html.ShouldContain("&lt;b&gt;Acme&lt;/b&gt; &amp; co is closed.");
        email.Subject.ShouldBe("<b>Acme</b> & co will be deleted on 22 Nov 2026");
    }

    // The key is the Owner's warning of this purge, so an event that arrives twice sends one email (O4), and a tenant closed again gets
    // a warning of its new purge.
    [Fact]
    public async Task SendTenantPurgeWarningEmail_AskedTwice_SendsUnderTheKeyOfTheOwnersWarningOfThisPurge()
    {
        var ready = Ready(Language.En);

        await SendAsync(ready);
        await SendAsync(ready with { EventId = Guid.CreateVersion7() });

        _channel.Sent.Select(email => email.IdempotencyKey).ShouldBe(
            [$"purge-warning/{TenantId}/{UserId}/{PurgesAt.UtcTicks}", $"purge-warning/{TenantId}/{UserId}/{PurgesAt.UtcTicks}"]);
    }

    [Fact]
    public async Task SendTenantPurgeWarningEmail_EmailChannelFails_IsAnError()
    {
        _channel.IsDown = true;

        var send = () => SendAsync(Ready(Language.En));

        await send.ShouldThrowAsync<HttpRequestException>();
    }

    private static TenantPurgeWarningReadyEvent Ready(Language language) =>
        new(Guid.CreateVersion7(), PurgesAt - TimeSpan.FromDays(7), TenantId, UserId, "ada@acme.test", "Acme Ltd", language, PurgesAt, null);

    private Task SendAsync(TenantPurgeWarningReadyEvent ready) => SendTenantPurgeWarningEmailHandler.HandleAsync(ready, _channel, Cancellation);

    private sealed class RecordingEmailChannel : IEmailChannel
    {
        public List<EmailMessage> Sent { get; } = [];

        public bool IsDown { get; set; }

        public Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
        {
            if (IsDown)
            {
                throw new HttpRequestException("The email channel is down.");
            }

            Sent.Add(email);
            return Task.CompletedTask;
        }
    }
}
