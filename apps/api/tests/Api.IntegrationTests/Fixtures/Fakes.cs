using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ControlPlane.Application.Ports;
using Notifications.Application;
using Notifications.Application.Ports;

namespace Api.IntegrationTests.Fixtures;

// Clerk, as far as the application uses it. Clerk is invite-only: an address without an account gets Clerk's invitation, whose link
// leads to the accept link once the person signed up; an address with an account gets none.
internal sealed class FakeIdentityProvider : IIdentityProvider
{
    private readonly ConcurrentDictionary<string, string[]> _accounts = new();
    private int _failures;
    private int _calls;

    public void AddAccount(string externalUserId, params string[] verifiedEmails) => _accounts[externalUserId] = verifiedEmails;

    // Lets a test look at the database at the moment the application asks for a user's verified addresses.
    public Func<Task>? WhileReadingVerifiedEmails { get; set; }

    // Every invitation Clerk created, with what it was given.
    public ConcurrentQueue<(string Email, Uri AcceptLink, DateTimeOffset ExpiresAt, string InvitationId)> SignUps { get; } = [];

    // Every invitation the application withdrew, and every user it deleted.
    public ConcurrentQueue<string> Withdrawn { get; } = [];

    public ConcurrentQueue<string> Deleted { get; } = [];

    // How many calls the application made; a refused one counts too.
    public int Calls => _calls;

    // While down, every call fails.
    public bool IsDown { get; set; }

    // The next calls fail, then Clerk recovers.
    public void FailNext(int calls) => _failures = calls;

    public Task<SignUpLink> AllowSignUpAsync(string email, Uri acceptLink, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        Answer();
        if (_accounts.Values.Any(emails => emails.Contains(email, StringComparer.OrdinalIgnoreCase)))
        {
            return Task.FromResult(new SignUpLink(acceptLink, null));
        }

        var invitationId = $"inv_{Guid.NewGuid():N}";
        SignUps.Enqueue((email, acceptLink, expiresAt, invitationId));
        return Task.FromResult(new SignUpLink(ClerkLink(invitationId, acceptLink), invitationId));
    }

    public Task WithdrawSignUpAsync(string invitationId, CancellationToken cancellationToken)
    {
        Answer();
        Withdrawn.Enqueue(invitationId);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<string>> FindVerifiedEmailsAsync(string externalUserId, CancellationToken cancellationToken)
    {
        if (WhileReadingVerifiedEmails is { } probe)
        {
            await probe();
        }

        return _accounts.GetValueOrDefault(externalUserId, []);
    }

    public Task DeleteUserAsync(string externalUserId, CancellationToken cancellationToken)
    {
        Answer();
        Deleted.Enqueue(externalUserId);
        return Task.CompletedTask;
    }

    // Clerk's link of an invitation: the accept link it leads to rides in it, so a test can follow it.
    public static Uri ClerkLink(string invitationId, Uri acceptLink) =>
        new($"https://clerk.test/v1/tickets/accept?ticket={invitationId}&redirect_url={Uri.EscapeDataString(acceptLink.ToString())}");

    // Where a link in an email leads: Clerk's link leads to the accept link it carries; any other link is the accept link.
    public static Uri AcceptLinkOf(Uri link) =>
        link.Host == "clerk.test"
            ? new Uri(Uri.UnescapeDataString(link.Query.TrimStart('?').Split('&').Single(pair => pair.StartsWith("redirect_url=", StringComparison.Ordinal))["redirect_url=".Length..]))
            : link;

    private void Answer()
    {
        Interlocked.Increment(ref _calls);
        if (IsDown || Interlocked.Decrement(ref _failures) >= 0)
        {
            throw new HttpRequestException("Clerk is down.");
        }
    }
}

// The email service at our port. Like Resend, it sends one email per idempotency key, however often it is asked (section 7).
internal sealed partial class FakeEmailChannel : IEmailChannel
{
    private readonly ConcurrentDictionary<string, EmailMessage> _sent = new();
    private int _failures;

    // Every request the application made, sent or refused.
    public ConcurrentQueue<(EmailMessage Email, long At)> Attempts { get; } = [];

    // The emails that went out.
    public IReadOnlyCollection<EmailMessage> Sent => [.. _sent.Values];

    // While down, every send fails.
    public bool IsDown { get; set; }

    // The next sends fail, then the channel recovers.
    public void FailNext(int sends) => _failures = sends;

    // Every send to this address fails, as when one recipient's mail server keeps refusing.
    public string? Refuses { get; set; }

    public Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        Attempts.Enqueue((email, TimeProvider.System.GetTimestamp()));
        if (IsDown || email.To == Refuses || Interlocked.Decrement(ref _failures) >= 0)
        {
            throw new HttpRequestException("The email service is down.");
        }

        _sent.TryAdd(email.IdempotencyKey, email);
        return Task.CompletedTask;
    }

    public Uri LinkSentTo(string email) => new(Link().Match(Sent.Single(sent => sent.To == email).Text).Value);

    // The invitation code of the accept link the email to the address leads to, through Clerk's link or directly.
    public string CodeSentTo(string email) => CodeOf(FakeIdentityProvider.AcceptLinkOf(LinkSentTo(email)));

    // The invitation code of the email sent for the invitation: its idempotency key is the invitation's.
    public string CodeSentFor(Guid invitationId) =>
        CodeOf(FakeIdentityProvider.AcceptLinkOf(new Uri(Link().Match(Sent.Single(sent => sent.IdempotencyKey == $"invite/{invitationId}").Text).Value)));

    private static string CodeOf(Uri acceptLink)
    {
        acceptLink.Query.ShouldStartWith("?code=");
        return Uri.UnescapeDataString(acceptLink.Query["?code=".Length..]);
    }

    [GeneratedRegex(@"https://\S+")]
    private static partial Regex Link();
}
