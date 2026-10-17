using ControlPlane.Contracts;
using Notifications.Application.Ports;
using Notifications.Contracts;

namespace Notifications.Application.Invitations.InvitationEmail;

// Step 3 of the tenant creation: the invitation email to a new tenant's first owner, in the invitation's language. The handler takes no
// DbContext, so Wolverine opens no transaction around the call to the email service; its retries are configured from the module's
// settings, and once they are spent the event goes to the dead letter queue.
public static class SendInvitationEmailHandler
{
    public static async Task<SendInvitationEmailReply> HandleAsync(
        OwnerInvitationReady ready,
        IEmailChannel channel,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        await channel.SendAsync(
            InvitationEmailTexts.Write(ready.Email, ready.TenantName, ready.Link, ready.Language, ready.Lifetime, ready.InvitationId),
            cancellationToken);

        var now = time.GetUtcNow();
        return new SendInvitationEmailReply(Guid.CreateVersion7(now), now, ready.TenantId, ready.InvitationId);
    }
}
