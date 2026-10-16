using ControlPlane.Contracts;
using Notifications.Application;
using Notifications.Application.Invitations.InvitationEmail;
using Notifications.Application.Ports;

namespace Notifications.UnitTests.Invitations.InvitationEmail;

// The Notifications module emails a person a member invited. It hears MemberInvited, which Audit hears too, and hands the email on to a
// command of its own, so the email's retries may be scheduled (W5).
public sealed class MemberInvitationEmailTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly Guid InvitationId = new("0199a8f0-0000-7000-8000-000000000402");
    private static readonly Uri Link = new("https://app.test/invitations/accept?code=x");

    private readonly RecordingEmailChannel _channel = new();

    [Fact]
    public void QueueMemberInvitationEmail_MemberInvited_AsksForTheEmailToTheInvitedAddress()
    {
        var send = QueueMemberInvitationEmailHandler.Handle(Invited());

        send.ShouldBe(new SendMemberInvitationEmail(InvitationId, "grace@navy.test", "Acme Ltd", Link));
    }

    // The same text as the owner invitation: the tenant's name and the link.
    [Fact]
    public async Task SendMemberInvitationEmail_Asked_EmailsTheTenantsNameAndTheLink()
    {
        await SendMemberInvitationEmailHandler.HandleAsync(new SendMemberInvitationEmail(InvitationId, "grace@navy.test", "Acme Ltd", Link), _channel, Cancellation);

        var email = _channel.Sent.ShouldHaveSingleItem();
        email.To.ShouldBe("grace@navy.test");
        email.Subject.ShouldBe("You are invited to Acme Ltd");
        email.Text.ShouldBe($"You have been invited to join Acme Ltd. Open this link to accept the invitation:\n\n{Link}");
    }

    // The key is the invitation's, so an event that arrives twice does not send a second email (O4).
    [Fact]
    public async Task SendMemberInvitationEmail_AskedTwice_SendsUnderTheInvitationsIdempotencyKey()
    {
        var send = QueueMemberInvitationEmailHandler.Handle(Invited());

        await SendMemberInvitationEmailHandler.HandleAsync(send, _channel, Cancellation);
        await SendMemberInvitationEmailHandler.HandleAsync(send, _channel, Cancellation);

        _channel.Sent.Select(email => email.IdempotencyKey).ShouldBe([$"invite/{InvitationId}", $"invite/{InvitationId}"]);
    }

    private static MemberInvited Invited() => new(
        Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, Guid.CreateVersion7(), InvitationId, Guid.CreateVersion7(), "Member", null,
        "grace@navy.test", "Acme Ltd", Link);

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
