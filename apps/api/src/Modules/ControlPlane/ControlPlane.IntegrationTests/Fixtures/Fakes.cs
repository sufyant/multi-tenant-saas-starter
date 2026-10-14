using System.Collections.Concurrent;
using ControlPlane.Application.Ports;

namespace ControlPlane.IntegrationTests.Fixtures;

// Clerk, as far as the module uses it. Tests use email addresses and user ids of their own, so they share one instance.
public sealed class FakeIdentityProvider : IIdentityProvider
{
    // The profile of an account a test gives none.
    public static readonly UserProfile NoProfile = new(null, null, null, DateTimeOffset.UnixEpoch);

    private readonly ConcurrentDictionary<string, IdentityAccount> _accounts = new();

    public void AddAccount(string externalUserId, params string[] verifiedEmails) => AddAccount(externalUserId, NoProfile, verifiedEmails);

    public void AddAccount(string externalUserId, UserProfile profile, params string[] verifiedEmails) =>
        _accounts[externalUserId] = new IdentityAccount(verifiedEmails, profile);

    public Task<IdentityAccount> FindAccountAsync(string externalUserId, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.GetValueOrDefault(externalUserId, new IdentityAccount([], NoProfile)));
}
