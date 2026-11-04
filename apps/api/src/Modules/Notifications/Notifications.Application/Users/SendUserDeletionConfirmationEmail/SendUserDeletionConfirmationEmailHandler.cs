using ControlPlane.Contracts.Users;
using Notifications.Application.Email;

namespace Notifications.Application.Users.SendUserDeletionConfirmationEmail;

// The confirmation to a user who deleted their own account, at the address they had, in their language (GDPR, Article 12(3)). The event
// has this one handler, so its retries may be scheduled (W5); it takes the invitation email's retries. The key is the user's, so a copy
// sends one email.
public static class SendUserDeletionConfirmationEmailHandler
{
    public static Task HandleAsync(UserDeletionConfirmationReadyEvent ready, IEmailChannel channel, CancellationToken cancellationToken)
    {
        var email = EmailTemplate.Of("account-deleted", ready.Language).Write(ready.Email, $"account-deleted/{ready.UserId}", []);
        return channel.SendAsync(email, cancellationToken);
    }
}
