using ControlPlane.Contracts.Invitations;
using Notifications.Application.Email;
using Notifications.Application.Invitations.SendInvitationEmail;
using SharedKernel;
using Wolverine;

namespace Notifications.UnitTests.Invitations.SendInvitationEmail;

// The Notifications module emails an invited person: a new tenant's first Owner, an Owner the platform invites, or a person a member
// invites. It hears InvitationReadyEvent, which only it hears, so the email's retries may be scheduled (W5). The email is the generated
// invitation of packages/emails, in HTML and in plain text: who invites, to which tenant, with which role, and the moment the link stops
// working, in the inviter's time zone with its label, or in UTC without one.
public sealed class SendInvitationEmailHandlerTests
{
    private static readonly Guid InvitationId = new("0199a8f0-0000-7000-8000-000000000402");

    private static readonly Uri Link = new("https://web.example/invitations/accept?code=x");

    private static readonly DateTimeOffset ExpiresAt = new(2026, 11, 1, 13, 32, 0, TimeSpan.Zero);

    private readonly RecordingEmailChannel _channel = new();

    public static TheoryData<Language, string, string, string> Texts => new()
    {
        {
            Language.En,
            "Ayşe Yılmaz invited you to Northwind Traders",
            "Ayşe Yılmaz invited you to join Northwind Traders as Admin.",
            "Expires on 1 Nov 2026 at 13:32 Dublin (UTC+0)."
        },
        {
            Language.Tr,
            "Ayşe Yılmaz sizi Northwind Traders kuruluşuna davet etti",
            "Ayşe Yılmaz sizi Northwind Traders kuruluşuna Yönetici olarak katılmaya davet etti.",
            "Son geçerlilik: 1 Kas 2026, saat 13:32 Dublin (UTC+0)."
        },
        {
            Language.Ar,
            "دعوة من Ayşe Yılmaz إلى Northwind Traders",
            "لديك دعوة من Ayşe Yılmaz للانضمام إلى Northwind Traders بصفة مشرف.",
            "تنتهي صلاحية الدعوة في 1 نوفمبر 2026 الساعة 13:32 بتوقيت Dublin (UTC+0)."
        },
    };

    public static TheoryData<Language> Languages => [.. Enum.GetValues<Language>()];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendInvitationEmail_Asked_EmailsTheInvitationInHtmlAndText()
    {
        await SendAsync(Ready());

        var email = _channel.Sent.ShouldHaveSingleItem();
        email.To.ShouldBe("grace@navy.test");
        email.Subject.ShouldBe("Ayşe Yılmaz invited you to Northwind Traders");
        email.Text.ShouldBe(
            "Starter\n\nAyşe Yılmaz invited you to join Northwind Traders as Admin.\n\nAccept invitation:\n"
            + $"{Link}\n\nExpires on 1 Nov 2026 at 13:32 Dublin (UTC+0).\n\n"
            + "You received this because someone invited this address. If you weren’t expecting it, you can ignore it.");
        email.Html.ShouldContain($"href=\"{Link}\"");
        email.Html.ShouldContain("Expires on 1 Nov 2026 at 13:32 Dublin (UTC+0).");
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public async Task SendInvitationEmail_InTheInvitationsLanguage_WritesTheSubjectTheHeadingAndTheExpiry(
        Language language, string subject, string heading, string expiry)
    {
        await SendAsync(Ready() with { Language = language });

        var email = _channel.Sent.ShouldHaveSingleItem();
        email.Subject.ShouldBe(subject);
        email.Text.Split("\n\n")[1..].ShouldBe([heading, $"{Messages(language).Button}:\n{Link}", expiry, Messages(language).Footer]);
    }

    // Every generated invitation in every language has exactly the placeholders the handler fills; a missing or an extra one fails here.
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task SendInvitationEmail_EveryLanguage_FillsEveryPlaceholder(Language language)
    {
        await SendAsync(Ready() with { Language = language });

        var email = _channel.Sent.ShouldHaveSingleItem();
        email.Html.ShouldStartWith("<!DOCTYPE html");
        $"{email.Subject}{email.Html}{email.Text}".ShouldNotContain("{{");
    }

    // The moment is in the inviter's time zone, named by the zone's city and its offset at that moment, as the purge warning names it. The
    // date is the zone's, so Honolulu's expiry comes the day before.
    [Theory]
    [InlineData("Europe/Istanbul", "Expires on 1 Nov 2026 at 16:32 Istanbul (UTC+3).")]
    [InlineData("Asia/Kolkata", "Expires on 1 Nov 2026 at 19:02 Kolkata (UTC+5:30).")]
    [InlineData("America/Argentina/Buenos_Aires", "Expires on 1 Nov 2026 at 10:32 Buenos Aires (UTC-3).")]
    [InlineData("Pacific/Honolulu", "Expires on 1 Nov 2026 at 03:32 Honolulu (UTC-10).")]
    public async Task SendInvitationEmail_AnInviterWithATimeZone_WritesTheExpiryInItWithItsLabel(string timeZone, string expiry)
    {
        await SendAsync(Ready() with { TimeZone = timeZone });

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[3].ShouldBe(expiry);
    }

    [Fact]
    public async Task SendInvitationEmail_AMomentBeforeTheZonesMidnight_WritesTheZonesDay()
    {
        await SendAsync(Ready() with { ExpiresAt = new DateTimeOffset(2026, 11, 1, 5, 0, 0, TimeSpan.Zero), TimeZone = "Pacific/Honolulu" });

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[3].ShouldBe("Expires on 31 Oct 2026 at 19:00 Honolulu (UTC-10).");
    }

    [Fact]
    public async Task SendInvitationEmail_AnInviterWithoutATimeZone_WritesTheExpiryInUtc()
    {
        await SendAsync(Ready() with { TimeZone = null });

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[3].ShouldBe("Expires on 1 Nov 2026 at 13:32 UTC.");
    }

    // A zone the image does not know, as an old name that a later release of the time zone data drops, writes the moment in UTC.
    [Fact]
    public async Task SendInvitationEmail_AnInviterWithAnUnknownTimeZone_WritesTheExpiryInUtc()
    {
        await SendAsync(Ready() with { TimeZone = "Mars/Olympus_Mons" });

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[3].ShouldBe("Expires on 1 Nov 2026 at 13:32 UTC.");
    }

    // Each language writes UTC in its own words.
    [Theory]
    [InlineData(Language.Tr, "Son geçerlilik: 1 Kas 2026, saat 13:32 UTC.")]
    [InlineData(Language.Ar, "تنتهي صلاحية الدعوة في 1 نوفمبر 2026 الساعة 13:32 بالتوقيت العالمي (UTC).")]
    public async Task SendInvitationEmail_WithoutATimeZoneInALanguage_WritesUtcInItsWords(Language language, string expiry)
    {
        await SendAsync(Ready() with { Language = language, TimeZone = null });

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[3].ShouldBe(expiry);
    }

    // Every month of the year, in each language, as the web writes a date (F18 in the web app's architecture).
    [Theory]
    [InlineData(Language.En, 9, "Expires on 1 Sept 2026 at 13:32 UTC.")]
    [InlineData(Language.En, 12, "Expires on 1 Dec 2026 at 13:32 UTC.")]
    [InlineData(Language.Tr, 2, "Son geçerlilik: 1 Şub 2026, saat 13:32 UTC.")]
    [InlineData(Language.Ar, 5, "تنتهي صلاحية الدعوة في 1 مايو 2026 الساعة 13:32 بالتوقيت العالمي (UTC).")]
    public async Task SendInvitationEmail_AMonth_WritesItsNameInTheLanguage(Language language, int month, string expiry)
    {
        await SendAsync(Ready() with { Language = language, ExpiresAt = new DateTimeOffset(2026, month, 1, 13, 32, 0, TimeSpan.Zero), TimeZone = null });

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[3].ShouldBe(expiry);
    }

    // The platform invites a tenant's Owner: the email names the brand, never the system admin.
    [Theory]
    [InlineData(Language.En, "Starter invited you to Northwind Traders", "Starter invited you to join Northwind Traders as Owner.")]
    [InlineData(Language.Tr, "Starter sizi Northwind Traders kuruluşuna davet etti", "Starter sizi Northwind Traders kuruluşuna Sahip olarak katılmaya davet etti.")]
    [InlineData(Language.Ar, "دعوة من Starter إلى Northwind Traders", "لديك دعوة من Starter للانضمام إلى Northwind Traders بصفة مالك.")]
    public async Task SendInvitationEmail_FromThePlatform_NamesTheBrand(Language language, string subject, string heading)
    {
        await SendAsync(Ready() with { Language = language, InviterName = null, RoleName = "owner" });

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.Subject, email.Text.Split("\n\n")[1]).ShouldBe((subject, heading));
    }

    // A built-in role has its name in each language; a custom role keeps the name the Owner typed.
    [Theory]
    [InlineData(Language.En, "member", true, "as Member.")]
    [InlineData(Language.Tr, "owner", true, "Sahip olarak katılmaya davet etti.")]
    [InlineData(Language.Ar, "member", true, "بصفة عضو.")]
    [InlineData(Language.En, "Field agent", false, "as Field agent.")]
    [InlineData(Language.Tr, "Field agent", false, "Field agent olarak katılmaya davet etti.")]
    [InlineData(Language.Ar, "Field agent", false, "بصفة Field agent.")]
    public async Task SendInvitationEmail_ARole_WritesABuiltInRoleInTheLanguageAndACustomRoleAsTyped(
        Language language, string roleName, bool builtIn, string headingEnd)
    {
        await SendAsync(Ready() with { Language = language, RoleName = roleName, RoleBuiltIn = builtIn });

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[1].ShouldEndWith(headingEnd);
    }

    // A custom role named like a built-in one is still the name the Owner typed.
    [Fact]
    public async Task SendInvitationEmail_ACustomRoleNamedLikeABuiltInOne_WritesItAsTyped()
    {
        await SendAsync(Ready() with { Language = Language.Tr, RoleName = "admin", RoleBuiltIn = false });

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[1].ShouldEndWith("admin olarak katılmaya davet etti.");
    }

    [Fact]
    public async Task SendInvitationEmail_ABuiltInRoleTheEmailsDoNotKnow_IsAnError()
    {
        var send = () => SendAsync(Ready() with { RoleName = "auditor", RoleBuiltIn = true });

        await send.ShouldThrowAsync<InvalidOperationException>();
    }

    // The names a person typed are data, never markup.
    [Fact]
    public async Task SendInvitationEmail_ANameWithHtml_EscapesItInTheHtmlAndKeepsItInTheText()
    {
        await SendAsync(Ready() with { InviterName = "<b>Ayşe</b> & co", TenantName = "R&D <Labs>" });

        var email = _channel.Sent.ShouldHaveSingleItem();
        email.Html.ShouldContain("&lt;b&gt;Ayşe&lt;/b&gt; &amp; co");
        email.Html.ShouldContain("R&amp;D &lt;Labs&gt;");
        email.Html.ShouldNotContain("<b>Ayşe</b>");
        email.Text.Split("\n\n")[1].ShouldBe("<b>Ayşe</b> & co invited you to join R&D <Labs> as Admin.");
    }

    [Fact]
    public async Task SendInvitationEmail_ALinkThatIsNotHttps_IsAnError()
    {
        var send = () => SendAsync(Ready() with { Link = new Uri("http://web.example/invitations/accept?code=x") });

        await send.ShouldThrowAsync<InvalidOperationException>();
        _channel.Sent.ShouldBeEmpty();
    }

    // The key is the invitation's, so an event that arrives twice does not send a second email (O4).
    [Fact]
    public async Task SendInvitationEmail_AskedTwice_SendsUnderTheInvitationsIdempotencyKey()
    {
        var ready = Ready();

        await SendAsync(ready);
        await SendAsync(ready);

        _channel.Sent.Select(email => email.IdempotencyKey).ShouldBe([$"invite/{InvitationId}", $"invite/{InvitationId}"]);
    }

    [Fact]
    public async Task SendInvitationEmail_EmailChannelFails_IsAnError()
    {
        _channel.IsDown = true;

        var send = () => SendAsync(Ready());

        await send.ShouldThrowAsync<HttpRequestException>();
    }

    // The language travels as its tag, as in the API: a message holds "tr", not a number that a new member of the enum could move.
    [Fact]
    public void WriteInvitationReady_InTurkish_HoldsTheLanguagesTag()
    {
        var json = System.Text.Encoding.UTF8.GetString(new WolverineOptions().DefaultSerializer.WriteMessage(Ready() with { Language = Language.Tr }));

        json.ShouldContain("\"tr\"");
    }

    private static InvitationReadyEvent Ready() =>
        new(
            Guid.CreateVersion7(),
            DateTimeOffset.UnixEpoch,
            Guid.CreateVersion7(),
            InvitationId,
            "grace@navy.test",
            "Northwind Traders",
            Link,
            Language.En,
            TimeSpan.FromDays(7),
            "Ayşe Yılmaz",
            "admin",
            true,
            ExpiresAt,
            "Europe/Dublin");

    // The button and the footer of each language, as the boards write them.
    private static (string Button, string Footer) Messages(Language language) => new Dictionary<Language, (string, string)>
    {
        [Language.En] = ("Accept invitation", "You received this because someone invited this address. If you weren’t expecting it, you can ignore it."),
        [Language.Tr] = ("Daveti kabul et", "Bu e-postayı, biri bu adresi davet ettiği için aldınız. Beklemiyorsanız görmezden gelebilirsiniz."),
        [Language.Ar] = ("قبول الدعوة", "وصلتك هذه الرسالة لأن أحدهم دعا هذا العنوان. إذا لم تكن تتوقعها، فيمكنك تجاهلها."),
    }[language];

    private Task SendAsync(InvitationReadyEvent ready) => SendInvitationEmailHandler.HandleAsync(ready, _channel, Cancellation);

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
