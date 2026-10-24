using ControlPlane.Contracts.Tenants;
using Notifications.Application.Email;
using Notifications.Application.Tenants.SendTenantPurgeWarningEmail;
using SharedKernel;

namespace Notifications.UnitTests.Tenants.SendTenantPurgeWarningEmail;

// The Notifications module warns each Owner of a closed tenant before its purge, in the Owner's own language: the tenant will be deleted
// with its data on a date, in a number of days (docs/flows/tenant-purge.md). It hears TenantPurgeWarningReadyEvent, which only it hears, so
// the email's retries may be scheduled (W5). The moment is in the Owner's own time zone, named with its offset as the UI names it; without
// a zone, or with one the image does not know, it is in UTC (section 7).
public sealed class SendTenantPurgeWarningEmailHandlerTests
{
    private static readonly Guid TenantId = new("0199a8f0-0000-7000-8000-000000000801");

    private static readonly Guid UserId = new("0199a8f0-0000-7000-8000-000000000802");

    private static readonly DateTimeOffset PurgesAt = new(2026, 11, 22, 9, 30, 0, TimeSpan.Zero);

    private static readonly TimeSpan SevenDays = TimeSpan.FromDays(7);

    private readonly RecordingEmailChannel _channel = new();

    public static TheoryData<Language, string, string> Texts => new()
    {
        {
            Language.En,
            "Acme Ltd will be deleted in 7 days",
            "Your organisation Acme Ltd is closed. It will be deleted with all its data in 7 days, on 2026-11-22 at 09:30 UTC. After that, "
            + "nobody can reopen it.\n\nTo keep it, ask the platform's administrators to reopen it before then."
        },
        {
            Language.Tr,
            "Acme Ltd adlı kuruluş 7 gün içinde silinecek",
            "Acme Ltd adlı kuruluşunuz kapatıldı. Kuruluş tüm verileriyle birlikte 7 gün içinde, 2026-11-22 tarihinde 09:30 UTC'de silinecek. "
            + "Bundan sonra kimse onu yeniden açamaz.\n\nKuruluşu korumak için bu tarihten önce platform yöneticilerinden onu yeniden açmalarını isteyin."
        },
        {
            Language.Ar,
            "سيتم حذف Acme Ltd خلال 7 أيام",
            "تم إغلاق مؤسستك Acme Ltd. سيتم حذفها مع جميع بياناتها خلال 7 أيام، في 2026-11-22 الساعة 09:30 بالتوقيت العالمي (UTC). "
            + "بعد ذلك لا يمكن لأحد إعادة فتحها.\n\nللاحتفاظ بها، اطلب من مسؤولي المنصة إعادة فتحها قبل ذلك."
        },
    };

    public static TheoryData<Language> Languages => [.. Enum.GetValues<Language>()];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendTenantPurgeWarningEmail_Asked_EmailsTheOwnersAddress()
    {
        await SendAsync(Ready(Language.En, SevenDays));

        _channel.Sent.ShouldHaveSingleItem().To.ShouldBe("ada@acme.test");
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public async Task SendTenantPurgeWarningEmail_InTheOwnersLanguage_WritesTheTenantTheDaysAndTheDate(Language language, string subject, string text)
    {
        await SendAsync(Ready(language, SevenDays));

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.Subject, email.Text).ShouldBe((subject, text));
    }

    // A new language is a member of the enum and its texts: one without texts fails here, not in production.
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task SendTenantPurgeWarningEmail_EveryLanguage_HasItsTexts(Language language)
    {
        await SendAsync(Ready(language, SevenDays));

        _channel.Sent.ShouldHaveSingleItem().Text.ShouldContain("2026-11-22");
    }

    // The moment in the Owner's zone, with the zone's city and its offset at that moment (F18 in the web app's architecture). The date is the
    // zone's, so Honolulu's purge comes the day before.
    [Theory]
    [InlineData("Europe/Istanbul", Language.En, "on 2026-11-22 at 12:30 Istanbul (UTC+3).")]
    [InlineData("Asia/Kolkata", Language.En, "on 2026-11-22 at 15:00 Kolkata (UTC+5:30).")]
    [InlineData("America/Argentina/Buenos_Aires", Language.En, "on 2026-11-22 at 06:30 Buenos Aires (UTC-3).")]
    [InlineData("Pacific/Honolulu", Language.En, "on 2026-11-21 at 23:30 Honolulu (UTC-10).")]
    [InlineData("Europe/Istanbul", Language.Tr, "2026-11-22 tarihinde Istanbul (UTC+3) saatiyle 12:30 itibarıyla silinecek.")]
    [InlineData("Europe/Istanbul", Language.Ar, "في 2026-11-22 الساعة 12:30 بتوقيت Istanbul (UTC+3).")]
    public async Task SendTenantPurgeWarningEmail_AnOwnerWithATimeZone_WritesTheMomentInItWithTheZoneAndItsOffset(
        string timeZone, Language language, string moment)
    {
        await SendAsync(Ready(language, SevenDays) with { TimeZone = timeZone });

        _channel.Sent.ShouldHaveSingleItem().Text.ShouldContain(moment);
    }

    // A zone the image does not know, as an old name that a later release of the time zone data drops, writes the moment in UTC.
    [Fact]
    public async Task SendTenantPurgeWarningEmail_AnOwnerWithAnUnknownTimeZone_WritesTheMomentInUtc()
    {
        await SendAsync(Ready(Language.En, SevenDays) with { TimeZone = "Mars/Olympus_Mons" });

        _channel.Sent.ShouldHaveSingleItem().Text.ShouldContain("on 2026-11-22 at 09:30 UTC.");
    }

    // The days are the whole days nearest to the time left, at least one, with each language's plural forms (Unicode CLDR, "Language
    // Plural Rules"): the warning arrives a little after its moment, so 6 days and 23 hours is 7 days.
    [Theory]
    [InlineData(Language.En, 1, "Acme Ltd will be deleted in 1 day")]
    [InlineData(Language.En, 2, "Acme Ltd will be deleted in 2 days")]
    [InlineData(Language.Tr, 1, "Acme Ltd adlı kuruluş 1 gün içinde silinecek")]
    [InlineData(Language.Ar, 1, "سيتم حذف Acme Ltd خلال يوم واحد")]
    [InlineData(Language.Ar, 2, "سيتم حذف Acme Ltd خلال يومين")]
    [InlineData(Language.Ar, 11, "سيتم حذف Acme Ltd خلال 11 يومًا")]
    public async Task SendTenantPurgeWarningEmail_ATimeLeftOfDays_SaysItInTheLanguagesPluralForm(Language language, int days, string subject)
    {
        await SendAsync(Ready(language, TimeSpan.FromDays(days) - TimeSpan.FromHours(1)));

        _channel.Sent.ShouldHaveSingleItem().Subject.ShouldBe(subject);
    }

    // The key is the Owner's warning of this purge, so an event that arrives twice sends one email (O4), and a tenant closed again gets
    // a warning of its new purge.
    [Fact]
    public async Task SendTenantPurgeWarningEmail_AskedTwice_SendsUnderTheKeyOfTheOwnersWarningOfThisPurge()
    {
        var ready = Ready(Language.En, SevenDays);

        await SendAsync(ready);
        await SendAsync(ready with { EventId = Guid.CreateVersion7() });

        _channel.Sent.Select(email => email.IdempotencyKey).ShouldBe(
            [$"purge-warning/{TenantId}/{UserId}/{PurgesAt.UtcTicks}", $"purge-warning/{TenantId}/{UserId}/{PurgesAt.UtcTicks}"]);
    }

    [Fact]
    public async Task SendTenantPurgeWarningEmail_EmailChannelFails_IsAnError()
    {
        _channel.IsDown = true;

        var send = () => SendAsync(Ready(Language.En, SevenDays));

        await send.ShouldThrowAsync<HttpRequestException>();
    }

    private static TenantPurgeWarningReadyEvent Ready(Language language, TimeSpan timeLeft) =>
        new(Guid.CreateVersion7(), PurgesAt - timeLeft, TenantId, UserId, "ada@acme.test", "Acme Ltd", language, PurgesAt, null);

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
