using System.Collections.Concurrent;
using ControlPlane.Application.Ports;

namespace ControlPlane.IntegrationTests.Fixtures;

// Clerk, as far as the module uses it. Tests use email addresses and user ids of their own, so they share one instance.
public sealed class FakeIdentityProvider : IIdentityProvider
{
    private readonly ConcurrentDictionary<string, string[]> _accounts = new();

    // Every sign-up Clerk allowed, with the invitation it created.
    public ConcurrentQueue<(string Email, Uri AcceptLink, DateTimeOffset ExpiresAt, string InvitationId)> SignUps { get; } = [];

    public ConcurrentQueue<string> Withdrawn { get; } = [];

    public void AddAccount(string externalUserId, params string[] verifiedEmails) => _accounts[externalUserId] = verifiedEmails;

    // Like Clerk, an address that belongs to an account needs no invitation; any other gets one, whose link leads to the accept link.
    public Task<SignUpLink> AllowSignUpAsync(string email, Uri acceptLink, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        if (_accounts.Values.Any(emails => emails.Contains(email, StringComparer.OrdinalIgnoreCase)))
        {
            return Task.FromResult(new SignUpLink(acceptLink, null));
        }

        var invitationId = $"inv_{Guid.NewGuid():N}";
        SignUps.Enqueue((email, acceptLink, expiresAt, invitationId));
        return Task.FromResult(new SignUpLink(
            new Uri($"https://clerk.test/v1/tickets/accept?ticket={invitationId}&redirect_url={Uri.EscapeDataString(acceptLink.ToString())}"),
            invitationId));
    }

    public Task WithdrawSignUpAsync(string invitationId, CancellationToken cancellationToken)
    {
        Withdrawn.Enqueue(invitationId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> FindVerifiedEmailsAsync(string externalUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(_accounts.GetValueOrDefault(externalUserId, []));

    public Task DeleteUserAsync(string externalUserId, CancellationToken cancellationToken) => Task.CompletedTask;
}
