using ControlPlane.Contracts.Users;
using Notifications.Application.Email;

namespace Notifications.Application.Users.SendEmailChangeNoticeEmail;

// The notice to the old address after a change of address, in the user's language: the new address masked, and the brand's support
// contact, so a person whose account someone took can act (A7). The event has this one handler, so its retries may be scheduled (W5); it
// takes the invitation email's retries. The key is the code that proved the new address, so a copy sends one email.
public static class SendEmailChangeNoticeEmailHandler
{
    public static Task HandleAsync(EmailChangeNoticeReadyEvent ready, IEmailChannel channel, CancellationToken cancellationToken)
    {
        var email = EmailTemplate.Of("email-changed", ready.Language).Write(
            ready.OldEmail,
            $"email-changed/{ready.UserId}/{ready.CodeId}",
            [EmailValue.Text("newEmail", MaskedEmail.Of(ready.NewEmail)), EmailValue.Contact("supportUrl", EmailTemplate.Brand.SupportUrl)]);
        return channel.SendAsync(email, cancellationToken);
    }
}
