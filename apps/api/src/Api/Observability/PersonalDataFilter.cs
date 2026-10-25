using System.Text.RegularExpressions;
using Sentry;

namespace Api.Observability;

// Personal data never leaves the process in an error event (section 7, "Error tracking"). Every text of an event, and every breadcrumb,
// loses its email addresses and invitation tokens before the SDK sends it. A log names no email address, but an exception's message, a
// URL or a value of a third party may.
internal static partial class PersonalDataFilter
{
    private const string AddressMark = "[email]";

    private const string TokenMark = "[token]";

    public static SentryEvent Apply(SentryEvent sentryEvent)
    {
        if (sentryEvent.Message is { } message)
        {
            message.Message = Replace(message.Message);
            message.Formatted = Replace(message.Formatted);
            message.Params = message.Params?.Select(value => value is string text ? Replace(text) : value).ToList();
        }

        foreach (var exception in sentryEvent.SentryExceptions ?? [])
        {
            exception.Value = Replace(exception.Value);
        }

        var request = sentryEvent.Request;
        request.Url = Replace(request.Url);
        request.QueryString = Replace(request.QueryString);
        foreach (var (name, value) in request.Headers.ToList())
        {
            request.Headers[name] = Replace(value);
        }

        foreach (var (key, value) in sentryEvent.Extra.Where(extra => extra.Value is string).ToList())
        {
            sentryEvent.SetExtra(key, Replace((string)value!));
        }

        foreach (var (key, value) in sentryEvent.Tags.ToList())
        {
            sentryEvent.SetTag(key, Replace(value));
        }

        return sentryEvent;
    }

    // A breadcrumb cannot change once it is made, so the filter makes it again.
    public static Breadcrumb Apply(Breadcrumb breadcrumb) => new(
        Replace(breadcrumb.Message) ?? "",
        breadcrumb.Type ?? "default",
        breadcrumb.Data?.ToDictionary(pair => pair.Key, pair => Replace(pair.Value)!),
        breadcrumb.Category,
        breadcrumb.Level);

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(text))]
    private static string? Replace(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        text = Address().Replace(text, AddressMark);
        text = TokenParameter().Replace(text, TokenMark);
        text = InvitationCode().Replace(text, TokenMark);
        return Jwt().Replace(text, TokenMark);
    }

    // An email address, also with its @ encoded in a URL (%40).
    [GeneratedRegex(@"[A-Z0-9._%+-]+(?:@|%40)[A-Z0-9-]+(?:\.[A-Z0-9-]+)*\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Address();

    // The value of a link's parameter that carries a token: the accept link's code (InvitationOptions.AcceptLink), and the identity
    // provider's ticket.
    [GeneratedRegex(@"(?<=[?&](?:code|ticket|__clerk_ticket)=)[^&#\s""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenParameter();

    // An invitation code on its own, <tenant id>.<secret> (InvitationCode), such as inside a link that is encoded in another link,
    // where it follows "%3D" with no word boundary.
    [GeneratedRegex(@"[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\.[A-Z0-9_-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InvitationCode();

    // A JSON Web Token: the identity provider's invitation ticket and a session token.
    [GeneratedRegex(@"\beyJ[A-Z0-9_-]+\.[A-Z0-9_-]+\.[A-Z0-9_-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Jwt();
}
