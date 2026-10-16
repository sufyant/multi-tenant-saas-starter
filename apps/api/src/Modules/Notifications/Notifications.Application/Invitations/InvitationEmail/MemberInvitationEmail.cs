using ControlPlane.Contracts;
using Notifications.Application.Ports;

namespace Notifications.Application.Invitations.InvitationEmail;

/// <summary>
/// Notifications' own command to email a person a member invited. It has one handler, so its retries may be scheduled; MemberInvited has
/// two, Audit's and this module's, and a scheduled retry of it could run in a web host (W5). It carries the invitation's link, as the
/// event does; see the deviations list in ARCHITECTURE.md.
/// </summary>
public sealed record SendMemberInvitationEmail(Guid InvitationId, string Email, string TenantName, Uri Link);

// Hands the invitation email on to the module's own command, in the same delivery: the handler only returns it, so nothing leaves the
// process and it needs no retry of its own.
public static class QueueMemberInvitationEmailHandler
{
    public static SendMemberInvitationEmail Handle(MemberInvited invited) => new(invited.InvitationId, invited.Email, invited.TenantName, invited.Link);
}

// The invitation email to a person a member invited, with the text of the owner invitation's. The handler takes no DbContext, so
// Wolverine opens no transaction around the call to the email service. It has the owner invitation email's retries; once they are spent
// the command goes to the dead letter queue, and the invitation stays pending: the inviter can invite again, which replaces it (K2). It
// sends no reply, as no saga waits for it.
public static class SendMemberInvitationEmailHandler
{
    // The idempotency key is the invitation's, so an event that arrives twice sends one email.
    public static Task HandleAsync(SendMemberInvitationEmail send, IEmailChannel channel, CancellationToken cancellationToken) =>
        channel.SendAsync(
            new EmailMessage(
                send.Email,
                $"You are invited to {send.TenantName}",
                $"You have been invited to join {send.TenantName}. Open this link to accept the invitation:\n\n{send.Link}",
                $"invite/{send.InvitationId}"),
            cancellationToken);
}
