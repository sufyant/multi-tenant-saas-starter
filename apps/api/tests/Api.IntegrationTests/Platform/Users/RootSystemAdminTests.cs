using System.Net;
using Tenancy;

namespace Api.IntegrationTests.Platform.Users;

// docs/flows/system-admins.md: ControlPlane:SystemAdmins:RootEmails names the root system admins. A person whose verified email
// address is in the list becomes a system admin at their first visit to the system door with a second factor (A6), at any time, also
// while other system admins exist. The platform granted it, so the platform's log names the platform as the actor (K3). Whether a
// system admin is a root is read from the list on each request; nothing stores it. Each test names its own root addresses, so the
// shared database's other system admins change nothing. GetUserTests and ListUsersTests read root.
public sealed class RootSystemAdminTests(Database database)
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task VisitTheSystemDoor_AListedPersonWhileOtherSystemAdminsExist_BecomesASystemAdmin()
    {
        await _testData.AddSystemAdminAsync();
        var root = RootEmail();
        await using var api = WithRoots(root);
        var person = Person(api, root.ToLowerInvariant());

        var response = await api.WaitingForMessagesAsync(() => api.CreateClient(person, secondFactor: true).GetAsync("/v1/platform/users", Cancellation));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await IsSystemAdminAsync(person)).ShouldBeTrue();
    }

    // A grant the door writes is recorded once, by the platform: the person did not ask for it, the configuration did.
    [Fact]
    public async Task VisitTheSystemDoor_AListedPersonTwice_IsRecordedOnceInThePlatformLogByThePlatform()
    {
        var root = RootEmail();
        await using var api = WithRoots(root);
        var person = Person(api, root);
        var client = api.CreateClient(person, secondFactor: true);

        await api.WaitingForMessagesAsync(() => client.GetAsync("/v1/platform/users", Cancellation));
        await api.WaitingForMessagesAsync(() => client.GetAsync("/v1/platform/users", Cancellation));

        var id = await _testData.UserIdOfAsync(person);
        (await _audit.PlatformLogAsync(id)).ShouldBe($"system_admin.granted|Platform|-|User|{id}|-");
        (await _audit.TenantLogAsync(id)).ShouldBeNull();
    }

    [Fact]
    public async Task VisitTheSystemDoor_AListedPersonWithoutASecondFactor_DoesNotBecomeASystemAdmin()
    {
        var root = RootEmail();
        await using var api = WithRoots(root);
        var person = Person(api, root);

        var response = await api.CreateClient(person, secondFactor: false).GetAsync("/v1/platform/users", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await IsSystemAdminAsync(person)).ShouldBeFalse();
    }

    // Only the identity provider's verified addresses count: one the person typed, or one waiting for verification, proves nothing.
    [Fact]
    public async Task VisitTheSystemDoor_TheListedAddressIsNotVerified_DoesNotBecomeASystemAdmin()
    {
        await using var api = WithRoots(RootEmail());
        var person = Person(api);

        var response = await api.CreateClient(person, secondFactor: true).GetAsync("/v1/platform/users", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await IsSystemAdminAsync(person)).ShouldBeFalse();
    }

    [Fact]
    public async Task VisitTheSystemDoor_APersonWithAnotherEmail_DoesNotBecomeASystemAdmin()
    {
        await using var api = WithRoots(RootEmail());
        var person = Person(api, RootEmail());

        var response = await api.CreateClient(person, secondFactor: true).GetAsync("/v1/platform/users", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await IsSystemAdminAsync(person)).ShouldBeFalse();
    }

    // A known user keeps their id, their address and their names; the grant only adds the row.
    [Fact]
    public async Task VisitTheSystemDoor_AKnownListedUser_BecomesASystemAdminWithTheSameId()
    {
        var root = RootEmail();
        var user = await _testData.AddUserAsync(email: root.ToLowerInvariant(), firstName: "Ada", lastName: "Lovelace");
        await using var api = WithRoots(root);
        api.Identity.AddAccount(user, root);

        await api.WaitingForMessagesAsync(() => api.CreateClient(user, secondFactor: true).GetAsync("/v1/platform/users", Cancellation));

        (await IsSystemAdminAsync(user)).ShouldBeTrue();
        (await _testData.UserOfAsync(user)).ShouldBe(new StoredUser(root.ToLowerInvariant(), "Ada", "Lovelace", Deleted: false, "en", null));
    }

    // Root is read from the stored address (SystemDoor.IsRoot). A known user whose stored address is another one is no root, even with a
    // listed address verified, so the door grants them nothing: a revocation could not keep out a system admin the door grants again.
    [Fact]
    public async Task VisitTheSystemDoor_AKnownUserWhoseStoredAddressIsNotListed_DoesNotBecomeASystemAdmin()
    {
        var root = RootEmail();
        var stored = $"{Guid.NewGuid():N}@example.com";
        var user = await _testData.AddUserAsync(email: stored);
        await using var api = WithRoots(root);
        api.Identity.AddAccount(user, stored, root);

        var response = await api.WaitingForMessagesAsync(() => api.CreateClient(user, secondFactor: true).GetAsync("/v1/platform/users", Cancellation));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await IsSystemAdminAsync(user)).ShouldBeFalse();
    }

    // A known user's address is ControlPlane's own (A7), so the door reads it and asks the identity provider nothing: a member who is no
    // system admin and comes with a second factor costs no call on each platform request.
    [Fact]
    public async Task VisitTheSystemDoor_AKnownUserWhoIsNotListed_AsksTheIdentityProviderNothing()
    {
        var user = await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com");
        await using var api = WithRoots(RootEmail());
        var reads = 0;
        api.Identity.WhileReadingVerifiedEmails = () => Task.FromResult(Interlocked.Increment(ref reads));

        var response = await api.CreateClient(user, secondFactor: true).GetAsync("/v1/platform/users", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        reads.ShouldBe(0);
    }

    [Fact]
    public async Task VisitTheSystemDoor_AKnownListedUser_BecomesASystemAdminWithoutAskingTheIdentityProvider()
    {
        var root = RootEmail();
        var user = await _testData.AddUserAsync(email: root.ToLowerInvariant());
        await using var api = WithRoots(root);
        var reads = 0;
        api.Identity.WhileReadingVerifiedEmails = () => Task.FromResult(Interlocked.Increment(ref reads));

        var response = await api.WaitingForMessagesAsync(() => api.CreateClient(user, secondFactor: true).GetAsync("/v1/platform/users", Cancellation));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await IsSystemAdminAsync(user)).ShouldBeTrue();
        reads.ShouldBe(0);
    }

    // A suspended user is not let in through the list: a system admin reactivates them first, as a grant by a system admin requires.
    [Fact]
    public async Task VisitTheSystemDoor_ASuspendedListedUser_DoesNotBecomeASystemAdmin()
    {
        var root = RootEmail();
        var user = await _testData.AddUserAsync(email: root.ToLowerInvariant());
        await _testData.SuspendUserAsync(user);
        await using var api = WithRoots(root);
        api.Identity.AddAccount(user, root);

        var response = await api.WaitingForMessagesAsync(() => api.CreateClient(user, secondFactor: true).GetAsync("/v1/platform/users", Cancellation));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await IsSystemAdminAsync(user)).ShouldBeFalse();
    }

    // Two pods may let the same person in at the same moment; one grant is written, and one entry.
    [Fact]
    public async Task VisitTheSystemDoor_TwoFirstVisitsAtOnce_GrantOnceAndRecordOnce()
    {
        var root = RootEmail();
        await using var api = WithRoots(root);
        var person = Person(api, root);
        var client = api.CreateClient(person, secondFactor: true);

        var visits = await api.WaitingForMessagesAsync(() => Task.WhenAll(client.GetAsync("/v1/platform/users", Cancellation), client.GetAsync("/v1/platform/users", Cancellation)));

        visits.Select(visit => visit.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.OK]);
        var id = await _testData.UserIdOfAsync(person);
        (await _audit.PlatformLogAsync(id)).ShouldBe($"system_admin.granted|Platform|-|User|{id}|-");
    }

    // An address in mixed case, so the list and the verified address meet only in their one form.
    private static string RootEmail() => $"Root.{Guid.NewGuid():N}@Example.com";

    // A person the identity provider knows, with the verified email addresses given, who is not a user yet.
    private static string Person(ApiFactory api, params string[] verifiedEmails)
    {
        var externalId = $"user_{Guid.NewGuid():N}";
        api.Identity.AddAccount(externalId, verifiedEmails);
        return externalId;
    }

    private ApiFactory WithRoots(params string[] roots) =>
        new(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: roots.Select((root, index) => KeyValuePair.Create($"ControlPlane:SystemAdmins:RootEmails:{index}", (string?)root)).ToDictionary());

    private async Task<bool> IsSystemAdminAsync(string externalId) =>
        await database.ScalarAsync<bool>(
            $"SELECT EXISTS (SELECT FROM control_plane.system_admins JOIN control_plane.users ON users.id = system_admins.user_id WHERE users.external_id = '{externalId}')");
}
