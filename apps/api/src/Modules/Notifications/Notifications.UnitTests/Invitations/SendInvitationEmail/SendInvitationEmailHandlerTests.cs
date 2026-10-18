using ControlPlane.Contracts.Invitations;
using Microsoft.Extensions.Time.Testing;
using Notifications.Application.Email;
using Notifications.Application.Invitations.SendInvitationEmail;
using Notifications.Contracts.Invitations;
using SharedKernel;

namespace Notifications.UnitTests.Invitations.SendInvitationEmail;

// The Notifications module sends the invitation email when ControlPlane announces that a tenant's first owner can be invited.
public sealed class SendInvitationEmailHandlerTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly Guid TenantId = new("0199a8f0-0000-7000-8000-000000000201");
    private static readonly Guid InvitationId = new("0199a8f0-0000-7000-8000-000000000401");
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
    private static readonly Uri Link = new("https://clerk.test/invitations/inv_1");

    private readonly RecordingEmailChannel _channel = new();
    private readonly FakeTimeProvider _time = new(Now);

    [Fact]
    public async Task SendInvitation_InvitationReady_EmailsTheOwnerTheTenantsNameAndTheLink()
    {
        await SendAsync(Ready());

        var email = _channel.Sent.ShouldHaveSingleItem();
        email.To.ShouldBe("owner@acme.test");
        email.Subject.ShouldContain("Acme Ltd");
        email.Text.ShouldContain("Acme Ltd");
        email.Text.ShouldContain(Link.ToString());
    }

    // The key is the invitation's, never a new one, so an event that arrives twice does not send a second email (O4).
    [Fact]
    public async Task SendInvitation_InvitationReady_SendsUnderTheInvitationsIdempotencyKey()
    {
        await SendAsync(Ready());
        await SendAsync(Ready());

        _channel.Sent.Select(email => email.IdempotencyKey).ShouldBe([$"invite/{InvitationId}", $"invite/{InvitationId}"]);
    }

    [Fact]
    public async Task SendInvitation_EmailSent_AnnouncesItForTheInvitation()
    {
        var sent = await SendAsync(Ready());

        sent.ShouldSatisfyAllConditions(
            announced => announced.TenantId.ShouldBe(TenantId),
            announced => announced.InvitationId.ShouldBe(InvitationId),
            announced => announced.OccurredAt.ShouldBe(Now),
            announced => announced.EventId.ShouldNotBe(Guid.Empty));
    }

    [Fact]
    public async Task SendInvitation_EmailChannelFails_IsAnError()
    {
        _channel.IsDown = true;

        var send = () => SendAsync(Ready());

        await send.ShouldThrowAsync<HttpRequestException>();
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public async Task SendInvitationEmail_InTheInvitationsLanguage_WritesTheSubjectTheLinkAndTheLifetime(Language language, string subject, string text)
    {
        await SendOwnerAsync(OwnerReady(language, SevenDays));

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.Subject, email.Text).ShouldBe((subject, text));
    }

    private static OwnerInvitationReadyEvent Ready() =>
        new(Guid.CreateVersion7(), Now, TenantId, InvitationId, "owner@acme.test", "Acme Ltd", Link, Language.En, TimeSpan.FromDays(7));

    private Task<SendInvitationEmailReply> SendAsync(OwnerInvitationReadyEvent ready) =>
        SendInvitationEmailHandler.HandleAsync(ready, _channel, _time, Cancellation);

    // The email channel, as a system we do not own, at our port.
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

    private static readonly TimeSpan SevenDays = TimeSpan.FromDays(7);

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

    private Task<SendInvitationEmailReply> SendOwnerAsync(OwnerInvitationReadyEvent ready) =>
        SendInvitationEmailHandler.HandleAsync(ready, _channel, new FakeTimeProvider(DateTimeOffset.UnixEpoch), Cancellation);

    private static OwnerInvitationReadyEvent OwnerReady(Language language, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, Guid.CreateVersion7(), InvitationId, "owner@acme.test", "Acme Ltd", Link, language, lifetime);
}
