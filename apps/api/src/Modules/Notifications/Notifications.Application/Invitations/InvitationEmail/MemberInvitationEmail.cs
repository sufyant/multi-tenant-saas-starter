using ControlPlane.Contracts;
using Notifications.Application.Ports;

namespace Notifications.Application.Invitations.InvitationEmail;

// The invitation email to a person a member invited, in the invitation's language, with the text of the owner invitation's.
// ControlPlane publishes the event once the identity provider lets the address sign up, so the link works. The event has this one
// handler, so its retries may be scheduled (W5).
// The handler takes no DbContext, so Wolverine opens no transaction around the call to the email service. It has the owner invitation
// email's retries; once they are spent the event goes to the dead letter queue, and the invitation stays pending: the inviter can invite
// again, which replaces it (K2). It sends no reply, as no saga waits for it.
public static class SendMemberInvitationEmailHandler
{
    public static Task HandleAsync(MemberInvitationReadyEvent ready, IEmailChannel channel, CancellationToken cancellationToken) =>
        channel.SendAsync(
            InvitationEmailTexts.Write(ready.Email, ready.TenantName, ready.Link, ready.Language, ready.Lifetime, ready.InvitationId),
            cancellationToken);
}
