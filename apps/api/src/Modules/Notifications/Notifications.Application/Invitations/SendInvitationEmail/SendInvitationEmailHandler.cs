using ControlPlane.Contracts.Invitations;
using Notifications.Application.Email;

namespace Notifications.Application.Invitations.SendInvitationEmail;

// The invitation email, in the invitation's language: to a new tenant's first Owner, to an Owner the platform invites, and to a person a
// member invites, with one template. ControlPlane publishes the event once the identity provider lets the address sign up, so the link
// works. The email names the inviter, or the brand when the platform invites, and writes the expiry in the inviter's time zone. The event
// has this one handler, so its retries may be scheduled (W5). The handler takes no DbContext, so Wolverine opens no transaction around the
// call to the email service. Once its retries are spent the event goes to the dead letter queue, and the invitation stays pending: the
// inviter can invite again, which replaces it (K2). Each try first asks ControlPlane whether the invitation is still pending, in a
// transaction of the check's own that ends before the call (W1): an invitation revoked, replaced, cancelled, accepted or expired while
// its email waited for a retry sends no email, so no email carries a link that answers 404.
public static class SendInvitationEmailHandler
{
    public static async Task HandleAsync(
        InvitationReadyEvent ready, IPendingInvitationCheck invitations, IEmailChannel channel, CancellationToken cancellationToken)
    {
        if (!await invitations.IsPendingAsync(ready.TenantId, ready.InvitationId, cancellationToken))
        {
            return;
        }

        await channel.SendAsync(Write(ready), cancellationToken);
    }

    /// <summary>The email of the event, in its language. The idempotency key is the invitation's, so an event that arrives twice sends one email.</summary>
    public static EmailMessage Write(InvitationReadyEvent ready)
    {
        var language = EmailLanguage.Of(ready.Language);
        var (_, expiresAt) = language.Dates.Write(ready.ExpiresAt, ready.TimeZone);

        return EmailTemplate.Of("invitation", ready.Language).Write(
            ready.Email,
            $"invite/{ready.InvitationId}",
            [
                EmailValue.Text("inviterName", ready.InviterName ?? EmailTemplate.Brand.Name),
                EmailValue.Text("tenantName", ready.TenantName),
                EmailValue.Text("roleName", language.RoleName(ready.RoleName, ready.RoleBuiltIn)),
                EmailValue.Text("expiresAt", expiresAt),
                EmailValue.Link("link", ready.Link),
            ]);
    }
}
