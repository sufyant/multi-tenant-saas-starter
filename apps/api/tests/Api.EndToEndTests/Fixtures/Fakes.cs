using System.Collections.Concurrent;
using ControlPlane.Application.Ports;
using Notifications.Application;
using Notifications.Application.Ports;

namespace Api.EndToEndTests.Fixtures;

// Clerk, as far as the application uses it. The owner signs up like any other user, and the provider verifies their email, which
// is their primary address.
internal sealed class FakeIdentityProvider : IIdentityProvider
{
    private readonly ConcurrentDictionary<string, IdentityAccount> _accounts = new();

    public void SignUp(string externalUserId, string verifiedEmail) =>
        _accounts[externalUserId] = new IdentityAccount([verifiedEmail], new UserProfile(verifiedEmail, null, null, DateTimeOffset.UtcNow));

    public Task<IdentityAccount> FindAccountAsync(string externalUserId, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.GetValueOrDefault(externalUserId, new IdentityAccount([], new UserProfile(null, null, null, DateTimeOffset.UnixEpoch))));
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
