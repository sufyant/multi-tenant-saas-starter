using ControlPlane.Contracts.Invitations;
using Notifications.Application.Email;
using Notifications.Application.Invitations.SendMemberInvitationEmail;
using SharedKernel;

namespace Notifications.UnitTests.Invitations.InvitationEmail;

// The Notifications module emails a person a member invited. It hears MemberInvitationReadyEvent, which only it hears, so the email's retries
// may be scheduled (W5).
public sealed class MemberInvitationEmailTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly Guid InvitationId = new("0199a8f0-0000-7000-8000-000000000402");
    private static readonly Uri Link = new("https://app.test/invitations/accept?code=x");

    private readonly RecordingEmailChannel _channel = new();

    // The same text as the owner invitation: the tenant's name, the link and how long it works.
    [Fact]
    public async Task SendMemberInvitationEmail_Asked_EmailsTheTenantsNameAndTheLink()
    {
        await SendMemberInvitationEmailHandler.HandleAsync(Ready(), _channel, Cancellation);

        var email = _channel.Sent.ShouldHaveSingleItem();
        email.To.ShouldBe("grace@navy.test");
        email.Subject.ShouldBe("You are invited to Acme Ltd");
        email.Text.ShouldBe($"You have been invited to join Acme Ltd. Open this link to accept the invitation:\n\n{Link}\n\nThe link works for 7 days.");
    }

    // The key is the invitation's, so an event that arrives twice does not send a second email (O4).
    [Fact]
    public async Task SendMemberInvitationEmail_AskedTwice_SendsUnderTheInvitationsIdempotencyKey()
    {
        var ready = Ready();

        await SendMemberInvitationEmailHandler.HandleAsync(ready, _channel, Cancellation);
        await SendMemberInvitationEmailHandler.HandleAsync(ready, _channel, Cancellation);

        _channel.Sent.Select(email => email.IdempotencyKey).ShouldBe([$"invite/{InvitationId}", $"invite/{InvitationId}"]);
    }

    private static MemberInvitationReadyEvent Ready() =>
        new(
            Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, Guid.CreateVersion7(), InvitationId, "grace@navy.test", "Acme Ltd", Link, Language.En, TimeSpan.FromDays(7));

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
