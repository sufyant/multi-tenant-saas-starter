using System.Net;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Webhooks;

// The identity provider keeps the catalog's copy of a user's email address and name fresh through its webhook (A7, A8). A delivery
// carries no user; its signature proves the sender. Every valid event answers 204, because the sender retries any
// other answer.
public sealed class ReceiveClerkEventTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Earlier = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly Catalog _catalog = new(database);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero));
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ReceiveClerkEvent_UserUpdatedForAKnownUser_CopiesThePrimaryEmailAndName()
    {
        var user = await _catalog.AddUserAsync();

        var received = await SendAsync(ClerkDeliveries.Signed(ClerkDeliveries.UserUpdated(user, "ada@example.com", "Ada", "Lovelace", Later)));

        received.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _catalog.ProfileOfAsync(user)).ShouldBe(new StoredProfile("ada@example.com", "Ada", "Lovelace", Later, null));
    }

    [Fact]
    public async Task ReceiveClerkEvent_WithoutASignature_IsABadRequestAndChangesNothing()
    {
        var user = await _catalog.AddUserAsync();

        var received = await SendAsync(ClerkDeliveries.Unsigned(ClerkDeliveries.UserUpdated(user, "ada@example.com", "Ada", "Lovelace", Later)));

        received.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _catalog.ProfileOfAsync(user)).ShouldBe(new StoredProfile(null, null, null, null, null));
    }

    [Fact]
    public async Task ReceiveClerkEvent_SignedWithAnotherSecret_IsABadRequestAndChangesNothing()
    {
        var user = await _catalog.AddUserAsync();

        var received = await SendAsync(ClerkDeliveries.Signed(
            ClerkDeliveries.UserUpdated(user, "ada@example.com", "Ada", "Lovelace", Later), secret: ClerkDeliveries.AnotherSecret));

        received.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _catalog.ProfileOfAsync(user)).ShouldBe(new StoredProfile(null, null, null, null, null));
    }

    // Svix's tolerance is five minutes; a delivery signed ten minutes ago could be a replay.
    [Fact]
    public async Task ReceiveClerkEvent_StaleTimestamp_IsABadRequestAndChangesNothing()
    {
        var user = await _catalog.AddUserAsync();

        var received = await SendAsync(ClerkDeliveries.Signed(
            ClerkDeliveries.UserUpdated(user, "ada@example.com", "Ada", "Lovelace", Later), at: DateTimeOffset.UtcNow.AddMinutes(-10)));

        received.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _catalog.ProfileOfAsync(user)).ShouldBe(new StoredProfile(null, null, null, null, null));
    }

    // A Clerk account becomes a catalog user only through an invitation or as the first system admin.
    [Fact]
    public async Task ReceiveClerkEvent_UserUpdatedForAnUnknownUser_AnswersNoContentAndAddsNoUser()
    {
        var stranger = $"user_{Guid.NewGuid():N}";

        var received = await SendAsync(ClerkDeliveries.Signed(ClerkDeliveries.UserUpdated(stranger, "ada@example.com", "Ada", "Lovelace", Later)));

        received.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _catalog.CountAsync($"SELECT count(*) FROM control_plane.users WHERE external_id = '{stranger}'")).ShouldBe(0);
    }

    // Deliveries can arrive out of order: an older change that arrives late does not overwrite a newer one.
    [Fact]
    public async Task ReceiveClerkEvent_OlderUserUpdatedAfterANewerOne_KeepsTheNewer()
    {
        var user = await _catalog.AddUserAsync();
        await SendAsync(ClerkDeliveries.Signed(ClerkDeliveries.UserUpdated(user, "ada@example.com", "Ada", "Lovelace", Later)));

        var received = await SendAsync(ClerkDeliveries.Signed(ClerkDeliveries.UserUpdated(user, "old@example.com", "Old", "Name", Earlier)));

        received.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _catalog.ProfileOfAsync(user)).ShouldBe(new StoredProfile("ada@example.com", "Ada", "Lovelace", Later, null));
    }

    // The sender retries a delivery it is not sure of; the endpoint takes no idempotency key, because it sets a state (I1).
    [Fact]
    public async Task ReceiveClerkEvent_SameEventTwice_GivesTheSameRow()
    {
        var user = await _catalog.AddUserAsync();
        var payload = ClerkDeliveries.UserUpdated(user, "ada@example.com", "Ada", null, Later);
        await SendAsync(ClerkDeliveries.Signed(payload));
        var once = await _catalog.ProfileOfAsync(user);

        var received = await SendAsync(ClerkDeliveries.Signed(payload));

        received.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _catalog.ProfileOfAsync(user)).ShouldBe(once);
        once.ShouldBe(new StoredProfile("ada@example.com", "Ada", null, Later, null));
    }

    // The copy is erased, and the row stays: memberships and audit entries point to it.
    [Fact]
    public async Task ReceiveClerkEvent_UserDeleted_ErasesTheCopyAndKeepsTheMemberships()
    {
        var (tenantId, _) = await _catalog.AddTenantAsync();
        var user = await _catalog.AddMemberAsync(tenantId);
        await SendAsync(ClerkDeliveries.Signed(ClerkDeliveries.UserUpdated(user, "ada@example.com", "Ada", "Lovelace", Later)));

        var received = await SendAsync(ClerkDeliveries.Signed(ClerkDeliveries.UserDeleted(user)));

        received.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _catalog.ProfileOfAsync(user)).ShouldBe(new StoredProfile(null, null, null, Later, _time.GetUtcNow()));
        (await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM control_plane.memberships JOIN control_plane.users ON users.id = memberships.user_id WHERE users.external_id = '{user}'"))
            .ShouldBe(1);
    }

    // A change Clerk made before the deletion, delivered after it, does not bring the erased copy back.
    [Fact]
    public async Task ReceiveClerkEvent_UserUpdatedAfterUserDeleted_KeepsTheCopyErased()
    {
        var user = await _catalog.AddUserAsync();
        await SendAsync(ClerkDeliveries.Signed(ClerkDeliveries.UserDeleted(user)));

        var received = await SendAsync(ClerkDeliveries.Signed(ClerkDeliveries.UserUpdated(user, "ada@example.com", "Ada", "Lovelace", Later)));

        received.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _catalog.ProfileOfAsync(user)).ShouldBe(new StoredProfile(null, null, null, null, _time.GetUtcNow()));
    }

    // user.created changes nothing: the paths that make a catalog user fill the copy themselves. Any other type is ignored.
    [Theory]
    [InlineData("user.created")]
    [InlineData("session.created")]
    public async Task ReceiveClerkEvent_UserCreatedOrAnotherType_AnswersNoContentAndChangesNothing(string type)
    {
        var user = await _catalog.AddUserAsync();

        var received = await SendAsync(ClerkDeliveries.Signed(ClerkDeliveries.UserEvent(type, user, "ada@example.com", "Ada", "Lovelace", Later)));

        received.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _catalog.ProfileOfAsync(user)).ShouldBe(new StoredProfile(null, null, null, null, null));
    }

    // API4: a delivery has no user, so the general limit is kept for each client address.
    [Fact]
    public async Task ReceiveClerkEvent_OverTheLimitOfOneAddress_IsRejected()
    {
        await using var api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: new Dictionary<string, string?> { ["RateLimiting:PermitLimit"] = "2" });
        var client = api.CreateClient();
        var user = await _catalog.AddUserAsync();
        HttpResponseMessage[] allowed =
        [
            await client.SendAsync(ClerkDeliveries.Signed(ClerkDeliveries.UserDeleted(user)), Cancellation),
            await client.SendAsync(ClerkDeliveries.Signed(ClerkDeliveries.UserDeleted(user)), Cancellation),
        ];

        var rejected = await client.SendAsync(ClerkDeliveries.Signed(ClerkDeliveries.UserDeleted(user)), Cancellation);

        allowed.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.NoContent, HttpStatusCode.NoContent]);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    // The delivery carries no Authorization header: the webhook has no user authentication.
    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage delivery) => _api.CreateClient().SendAsync(delivery, Cancellation);
}
