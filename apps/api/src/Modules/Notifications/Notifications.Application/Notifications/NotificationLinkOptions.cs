namespace Notifications.Application.Notifications;

// Where the button of a notification email leads (section Notifications:Links), validated at startup by the module's infrastructure: the
// root of the web app for a tenant's and a personal notification, and of the platform app for the platform's. The bell builds its own
// links from the link kind (U1); an email can only carry a URL.
public sealed class NotificationLinkOptions
{
    public const string Section = "Notifications:Links";

    public Uri? WebUrl { get; set; }

    public Uri? PlatformUrl { get; set; }

    public static bool IsHttps(Uri? url) => url is { IsAbsoluteUri: true } && url.Scheme == Uri.UriSchemeHttps;
}
