using System.Collections.Concurrent;
using ControlPlane.Application.Identity;

namespace ControlPlane.IntegrationTests.Fixtures;

// Clerk, as far as the module uses it. Tests use email addresses and user ids of their own, so they share one instance.
public sealed class FakeIdentityProvider : IIdentityProvider
{
    private readonly ConcurrentDictionary<string, string[]> _accounts = new();

    // Every sign-up Clerk allowed, with the invitation it created.
    public ConcurrentQueue<(string Email, Uri AcceptLink, DateTimeOffset ExpiresAt, string InvitationId)> SignUps { get; } = [];

    public ConcurrentQueue<string> Withdrawn { get; } = [];

    // Every change of a user's address the application asked for.
    public ConcurrentQueue<(string ExternalUserId, string Email)> Replaced { get; } = [];

    public void AddAccount(string externalUserId, params string[] verifiedEmails) => _accounts[externalUserId] = verifiedEmails;

    // Like Clerk, an address that belongs to an account needs no invitation; any other gets one, whose link leads to the accept link.
    public Task<SignUpLink> OpenSignUpAsync(string email, Uri acceptLink, DateTimeOffset expiresAt, CancellationToken cancellationToken)
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

    public Task ReplaceEmailAsync(string externalUserId, string email, CancellationToken cancellationToken)
    {
        Replaced.Enqueue((externalUserId, email));
        return Task.CompletedTask;
    }

    public Task<bool> IsEmailTakenByAnotherAccountAsync(string email, string externalUserId, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.Any(account => account.Key != externalUserId && account.Value.Contains(email, StringComparer.OrdinalIgnoreCase)));

    public Task DeleteUserAsync(string externalUserId, CancellationToken cancellationToken) => Task.CompletedTask;
}

// A cache as one process's memory: it keeps each entry until a removal by its key or one of its tags, and never expires one.
public sealed class FakeHybridCache : Microsoft.Extensions.Caching.Hybrid.HybridCache
{
    private readonly ConcurrentDictionary<string, (object? Value, string[] Tags)> _entries = new();

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        Microsoft.Extensions.Caching.Hybrid.HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            return (T)entry.Value!;
        }

        var value = await factory(state, cancellationToken);
        _entries[key] = (value, [.. tags ?? []]);
        return value;
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        Microsoft.Extensions.Caching.Hybrid.HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        _entries[key] = (value, [.. tags ?? []]);
        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        foreach (var key in _entries.Where(entry => entry.Value.Tags.Contains(tag)).Select(entry => entry.Key).ToList())
        {
            _entries.TryRemove(key, out _);
        }

        return ValueTask.CompletedTask;
    }
}
