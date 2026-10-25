using ControlPlane.Contracts.Invitations;
using Notifications.Application.Email;

namespace Notifications.Application.Invitations.SendInvitationEmail;

// The invitation email, in the invitation's language: to a new tenant's first Owner, to an Owner the platform invites, and to a person a
// member invites, with one template. ControlPlane publishes the event once the identity provider lets the address sign up, so the link
// works. The email names the inviter, or the brand when the platform invites, and writes the expiry in the inviter's time zone. The event
// has this one handler, so its retries may be scheduled (W5). The handler takes no DbContext, so Wolverine opens no transaction around the
// call to the email service. Once its retries are spent the event goes to the dead letter queue, and the invitation stays pending: the
// inviter can invite again, which replaces it (K2).
public static class SendInvitationEmailHandler
{
    public static Task HandleAsync(InvitationReadyEvent ready, IEmailChannel channel, CancellationToken cancellationToken)
    {
        var language = EmailLanguage.Of(ready.Language);
        var (_, expiresAt) = language.Dates.Write(ready.ExpiresAt, ready.TimeZone);

        // The idempotency key is the invitation's, so an event that arrives twice sends one email.
        var email = EmailTemplate.Of("invitation", ready.Language).Write(
            ready.Email,
            $"invite/{ready.InvitationId}",
            [
                EmailValue.Text("inviterName", ready.InviterName ?? EmailTemplate.Brand.Name),
                EmailValue.Text("tenantName", ready.TenantName),
                EmailValue.Text("roleName", language.RoleName(ready.RoleName, ready.RoleBuiltIn)),
                EmailValue.Text("expiresAt", expiresAt),
                EmailValue.Link("link", ready.Link),
            ]);
        return channel.SendAsync(email, cancellationToken);
    }
}
