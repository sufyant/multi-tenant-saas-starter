using System.Text.RegularExpressions;
using Sentry;

namespace Api.Observability;

// The error tracker groups events into one error, and alerts once for a new error (section 7, "Error tracking", rule 9). It takes ids out
// of a message, so every dead letter of one message type would be one error, and a new cause of the same type would never alert. The
// fingerprint of a dead letter is its message type and its cause: the exception's type and its message without ids, addresses and
// numbers. So a new cause alerts once, and a repeat of the same cause joins its error.
internal static partial class DeadLetterFingerprint
{
    // The cause of a message that no node handles (UnknownMessages): it has no exception.
    private const string NoHandler = "no handler";

    public static SentryEvent Apply(SentryEvent sentryEvent)
    {
        var text = sentryEvent.Message?.Formatted ?? "";
        var match = WolverineDeadLetter().Match(text) is { Success: true } wolverine ? wolverine : UnknownMessageDeadLetter().Match(text);
        if (!match.Success)
        {
            return sentryEvent;
        }

        // The SDK lists the innermost exception first; the outermost one is the cause the handler threw.
        var exception = sentryEvent.SentryExceptions?.LastOrDefault();
        sentryEvent.SetFingerprint(
            "dead-letter",
            match.Groups["type"].Value.Split('.')[^1],
            exception?.Type ?? NoHandler,
            Normalize(exception?.Value ?? ""));
        return sentryEvent;
    }

    // The privacy filter ran first, so an address is already "[email]". Ids and numbers vary with each message of one cause.
    private static string Normalize(string message) => Number().Replace(Hex().Replace(Guid().Replace(message, "*"), "*"), "*");

    // Wolverine's log of the move: "Envelope {envelope} was moved to the error queue", where the envelope reads
    // "Envelope #<id>/CorrelationId=<id> (<full name of the message type>) from <source> to <destination>", in quotes.
    [GeneratedRegex(@"^Envelope ""?Envelope #\S+ \((?<type>[^)]+)\).* was moved to the error queue", RegexOptions.CultureInvariant)]
    private static partial Regex WolverineDeadLetter();

    // UnknownMessages' log: "No node handled {MessageType} after ...", with the type in quotes.
    [GeneratedRegex(@"^No node handled ""?(?<type>[^""\s]+)""? after .* moved to the dead letter queue", RegexOptions.CultureInvariant)]
    private static partial Regex UnknownMessageDeadLetter();

    [GeneratedRegex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Guid();

    [GeneratedRegex(@"\b[0-9a-f]{16,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Hex();

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex Number();
}
