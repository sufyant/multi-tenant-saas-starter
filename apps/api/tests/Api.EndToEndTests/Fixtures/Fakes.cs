using System.Collections.Concurrent;
using ControlPlane.Application.Identity;
using Notifications.Application.Email;

namespace Api.EndToEndTests.Fixtures;

// Clerk, as far as the application uses it. Clerk is invite-only: an address without an account gets Clerk's invitation, and the person
// signs up through its link, which verifies the invited address and leads to our accept link.
internal sealed class FakeIdentityProvider : IIdentityProvider
{
    private readonly ConcurrentDictionary<string, string[]> _accounts = new();
    private readonly ConcurrentDictionary<string, (string Email, Uri AcceptLink)> _invitations = new();

    public void SignUp(string externalUserId, string verifiedEmail) => _accounts[externalUserId] = [verifiedEmail];

    // The person opens the link of Clerk's invitation and signs up with the invited address; Clerk then sends them to the accept link,
    // with its ticket.
    public Uri SignUpThrough(Uri clerkLink, string externalUserId)
    {
        var invitationId = clerkLink.Query.TrimStart('?').Split('&').Single(pair => pair.StartsWith("ticket=", StringComparison.Ordinal))["ticket=".Length..];
        var (email, acceptLink) = _invitations[invitationId];
        SignUp(externalUserId, email);
        return new Uri($"{acceptLink}&__clerk_ticket={invitationId}");
    }

    public Task<SignUpLink> OpenSignUpAsync(string email, Uri acceptLink, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        if (_accounts.Values.Any(emails => emails.Contains(email, StringComparer.OrdinalIgnoreCase)))
        {
            return Task.FromResult(new SignUpLink(acceptLink, null));
        }

        var invitationId = $"inv_{Guid.NewGuid():N}";
        _invitations[invitationId] = (email, acceptLink);
        return Task.FromResult(new SignUpLink(new Uri($"https://clerk.test/v1/tickets/accept?ticket={invitationId}"), invitationId));
    }

    public Task WithdrawSignUpAsync(string invitationId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<string>> FindVerifiedEmailsAsync(string externalUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(_accounts.GetValueOrDefault(externalUserId, []));

    public Task DeleteUserAsync(string externalUserId, CancellationToken cancellationToken) => Task.CompletedTask;
}

// The email service, as the application sends through it.
internal sealed class FakeEmailChannel : IEmailChannel
{
    public ConcurrentBag<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        Sent.Add(email);
        return Task.CompletedTask;
    }
}
