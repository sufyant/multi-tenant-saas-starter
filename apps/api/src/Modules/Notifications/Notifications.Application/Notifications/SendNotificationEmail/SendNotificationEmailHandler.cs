using ControlPlane.Contracts.Users;
using Microsoft.Extensions.Options;
using Notifications.Application.Email;
using Notifications.Domain;
using Notifications.Domain.Notifications;

namespace Notifications.Application.Notifications.SendNotificationEmail;

// The generic notification email (U2, U4): the notification's title and body, rendered in the recipient's language and time zone as the
// bell renders them, with the names of its people at the moment of the event; one button to the web app, or to the platform app for the
// platform's notifications; and the footer of its type, which says why the email came (U5). The handler takes no DbContext, so Wolverine opens no transaction around the call to the email service. An email that fails is tried again and then goes to the dead letter
// queue (W11); the in-app notification stays.
public static class SendNotificationEmailHandler
{
    public static Task HandleAsync(
        SendNotificationEmailCommand send,
        IEmailChannel channel,
        IOptions<NotificationLinkOptions> links,
        CancellationToken cancellationToken)
    {
        var text = NotificationText.Render(send.Type, send.Values, send.People ?? new Dictionary<Guid, UserName>(), send.Language, send.TimeZone);
        var kind = NotificationCatalog.Of(send.Type);
        var link = (kind.Bell == NotificationBell.Platform ? links.Value.PlatformUrl : links.Value.WebUrl)!;
        List<EmailValue> values =
        [
            EmailValue.Text("title", text.Title),
            EmailValue.Text("body", text.Body),
            EmailValue.Link("link", link),
            EmailValue.Text("footer", NotificationText.FooterOf(send.Language, send.Type)),
        ];

        // The key is the notification's, so a copy of the message sends one email.
        var email = EmailTemplate.Of("notification", send.Language).Write(
            send.Email,
            $"notification/{send.NotificationId}",
            values);
        return channel.SendAsync(email, cancellationToken);
    }
}
