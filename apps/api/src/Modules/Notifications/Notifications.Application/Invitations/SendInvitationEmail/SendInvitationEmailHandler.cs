using ControlPlane.Contracts.Invitations;
using Notifications.Application.Email;

namespace Notifications.Application.Invitations.SendInvitationEmail;

// The invitation email, in the invitation's language: to a new tenant's first Owner, to an Owner the platform invites, and to a person a
// member invites, with one text. ControlPlane publishes the event once the identity provider lets the address sign up, so the link works.
// The event has this one handler, so its retries may be scheduled (W5). The handler takes no DbContext, so Wolverine opens no
// transaction around the call to the email service. Once its retries are spent the event goes to the dead letter queue, and the
// invitation stays pending: the inviter can invite again, which replaces it (K2).
public static class SendInvitationEmailHandler
{
    public static Task HandleAsync(InvitationReadyEvent ready, IEmailChannel channel, CancellationToken cancellationToken) =>
        channel.SendAsync(
            InvitationEmailTexts.Write(ready.Email, ready.TenantName, ready.Link, ready.Language, ready.Lifetime, ready.InvitationId),
            cancellationToken);
}
