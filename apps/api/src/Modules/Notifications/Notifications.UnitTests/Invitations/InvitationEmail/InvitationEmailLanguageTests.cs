using ControlPlane.Contracts;
using Microsoft.Extensions.Time.Testing;
using Notifications.Application;
using Notifications.Application.Invitations.InvitationEmail;
using Notifications.Application.Ports;
using Notifications.Contracts;
using SharedKernel;
using Wolverine;

namespace Notifications.UnitTests.Invitations.InvitationEmail;

// Decision 7 of step 20j: the member and the owner invitation emails say the same, in the invitation's language, in plain text. The
// email shows no date or time, so it needs no time zone: it says how long the link works, from the invitation's lifetime.
public sealed class InvitationEmailLanguageTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly Guid InvitationId = new("0199a8f0-0000-7000-8000-000000000403");
    private static readonly Uri Link = new("https://clerk.test/invitations/inv_1");
    private static readonly TimeSpan SevenDays = TimeSpan.FromDays(7);

    private readonly RecordingEmailChannel _channel = new();

    public static TheoryData<Language, string, string> Texts => new()
    {
        {
            Language.En,
            "You are invited to Acme Ltd",
            $"You have been invited to join Acme Ltd. Open this link to accept the invitation:\n\n{Link}\n\nThe link works for 7 days."
        },
        {
            Language.Tr,
            "Acme Ltd adlı kuruluşa davet edildiniz",
            $"Acme Ltd adlı kuruluşa katılmaya davet edildiniz. Daveti kabul etmek için bu bağlantıyı açın:\n\n{Link}\n\nBağlantı 7 gün geçerlidir."
        },
        {
            Language.Ar,
            "دعوة للانضمام إلى Acme Ltd",
            $"تمت دعوتك للانضمام إلى Acme Ltd. افتح هذا الرابط لقبول الدعوة:\n\n{Link}\n\nالرابط صالح لمدة 7 أيام."
        },
    };

    public static TheoryData<Language> Languages => [.. Enum.GetValues<Language>()];

    [Theory]
    [MemberData(nameof(Texts))]
    public async Task SendMemberInvitationEmail_InTheInvitationsLanguage_WritesTheSubjectTheLinkAndTheLifetime(Language language, string subject, string text)
    {
        await SendMemberAsync(MemberReady(language, SevenDays));

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.Subject, email.Text).ShouldBe((subject, text));
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public async Task SendInvitationEmail_InTheInvitationsLanguage_WritesTheSubjectTheLinkAndTheLifetime(Language language, string subject, string text)
    {
        await SendOwnerAsync(OwnerReady(language, SevenDays));

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.Subject, email.Text).ShouldBe((subject, text));
    }

    // A new language is a member of the enum and its texts: one without texts fails here, not in production.
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task SendMemberInvitationEmail_EveryLanguage_HasItsTexts(Language language)
    {
        await SendMemberAsync(MemberReady(language, SevenDays));

        _channel.Sent.ShouldHaveSingleItem().Text.ShouldContain(Link.ToString());
    }

    // The lifetime is said in whole days, with each language's plural forms (Unicode CLDR, "Language Plural Rules"). Arabic has six.
    [Theory]
    [InlineData(Language.En, 1, "The link works for 1 day.")]
    [InlineData(Language.En, 2, "The link works for 2 days.")]
    [InlineData(Language.Tr, 1, "Bağlantı 1 gün geçerlidir.")]
    [InlineData(Language.Tr, 30, "Bağlantı 30 gün geçerlidir.")]
    [InlineData(Language.Ar, 1, "الرابط صالح لمدة يوم واحد.")]
    [InlineData(Language.Ar, 2, "الرابط صالح لمدة يومين.")]
    [InlineData(Language.Ar, 3, "الرابط صالح لمدة 3 أيام.")]
    [InlineData(Language.Ar, 10, "الرابط صالح لمدة 10 أيام.")]
    [InlineData(Language.Ar, 11, "الرابط صالح لمدة 11 يومًا.")]
    [InlineData(Language.Ar, 99, "الرابط صالح لمدة 99 يومًا.")]
    [InlineData(Language.Ar, 100, "الرابط صالح لمدة 100 يوم.")]
    [InlineData(Language.Ar, 103, "الرابط صالح لمدة 103 أيام.")]
    public async Task SendMemberInvitationEmail_ALifetimeOfDays_SaysItInTheLanguagesPluralForm(Language language, int days, string lastLine)
    {
        await SendMemberAsync(MemberReady(language, TimeSpan.FromDays(days)));

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[^1].ShouldBe(lastLine);
    }

    // A part of a day is not promised: a lifetime of 7 days and 20 hours says 7 days.
    [Fact]
    public async Task SendMemberInvitationEmail_ALifetimeWithPartOfADay_SaysTheWholeDays()
    {
        await SendMemberAsync(MemberReady(Language.En, TimeSpan.FromDays(7) + TimeSpan.FromHours(20)));

        _channel.Sent.ShouldHaveSingleItem().Text.Split("\n\n")[^1].ShouldBe("The link works for 7 days.");
    }

    // The language travels as its tag, as in the API: a message holds "tr", not a number that a new member of the enum could move.
    [Fact]
    public void WriteMemberInvitationReady_InTurkish_HoldsTheLanguagesTag()
    {
        var json = System.Text.Encoding.UTF8.GetString(new WolverineOptions().DefaultSerializer.WriteMessage(MemberReady(Language.Tr, SevenDays)));

        json.ShouldContain("\"tr\"");
    }

    private Task SendMemberAsync(MemberInvitationReadyEvent ready) => SendMemberInvitationEmailHandler.HandleAsync(ready, _channel, Cancellation);

    private Task<SendInvitationEmailReply> SendOwnerAsync(OwnerInvitationReadyEvent ready) =>
        SendInvitationEmailHandler.HandleAsync(ready, _channel, new FakeTimeProvider(DateTimeOffset.UnixEpoch), Cancellation);

    private static MemberInvitationReadyEvent MemberReady(Language language, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, Guid.CreateVersion7(), InvitationId, "grace@navy.test", "Acme Ltd", Link, language, lifetime);

    private static OwnerInvitationReadyEvent OwnerReady(Language language, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, Guid.CreateVersion7(), InvitationId, "owner@acme.test", "Acme Ltd", Link, language, lifetime);

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
