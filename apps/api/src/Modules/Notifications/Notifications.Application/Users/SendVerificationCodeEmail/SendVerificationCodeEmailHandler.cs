using ControlPlane.Contracts.Users;
using Notifications.Application.Email;

namespace Notifications.Application.Users.SendVerificationCodeEmail;

// The email that carries a verification code, in the user's language: to the new address for a change, or to the user's own for the
// deletion of their account (section 6, "Verification codes"). The event has this one handler, so its retries may be scheduled (W5); it
// takes the invitation email's retries. The handler takes no DbContext, so Wolverine opens no transaction around the call. The key is the
// code's, so a copy of the event sends one email; a new code is a new email.
public static class SendVerificationCodeEmailHandler
{
    public static Task HandleAsync(VerificationCodeReadyEvent ready, IEmailChannel channel, CancellationToken cancellationToken)
    {
        var email = EmailTemplate.Of(ready.Kind == VerificationEmailKind.EmailChange ? "email-change-code" : "deletion-code", ready.Language)
            .Write(ready.Email, $"verification-code/{ready.CodeId}", [EmailValue.Text("verificationCode", ready.Code)]);
        return channel.SendAsync(email, cancellationToken);
    }
}
