using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Platform;

// GET /v1/platform/users: every catalog user, for the system admin, by email address, then id. Users belong to no tenant: a test that
// reads the whole list, or a status, has a database of its own, where no other test adds a user; any other test searches for a marker
// of its own.
public sealed class ListUsersTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly string _marker = Guid.NewGuid().ToString("N")[..12];
    private Catalog _catalog = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // A user without an email address, such as one the identity provider deleted, comes last. A deleted user stays deleted when they
    // were suspended too.
    [Fact]
    public async Task ListUsers_UsersInEveryStatus_ReturnsEachByEmailThenId()
    {
        await UseADatabaseOfItsOwnAsync();
        var random = Guid.NewGuid().ToString();
        var carol = await _catalog.AddUserAsync(email: "carol@example.com", firstName: "Carol", lastName: "Shaw");
        var bob = await _catalog.AddSystemAdminAsync(email: "bob@example.com");
        var alice = await _catalog.AddUserAsync(email: "alice@example.com", firstName: "Alice", lastName: "Ball");
        await _catalog.SuspendUserAsync(alice);
        var deletedLater = await _catalog.AddUserAsync(Guid.Parse($"f{random[1..]}"), email: "dave@example.com");
        await _catalog.DeleteUserAsync(deletedLater);
        await _catalog.SuspendUserAsync(deletedLater);
        var deletedEarlier = await _catalog.AddUserAsync(Guid.Parse($"0{random[1..]}"), email: "erin@example.com");
        await _catalog.DeleteUserAsync(deletedEarlier);

        var response = await _api.CreateClient(bob, secondFactor: true).GetAsync("/v1/platform/users", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Users(page).ShouldBe(
        [
            (await _catalog.UserIdOfAsync(alice), "alice@example.com", "Alice", "Ball", "suspended", false),
            (await _catalog.UserIdOfAsync(bob), "bob@example.com", null, null, "active", true),
            (await _catalog.UserIdOfAsync(carol), "carol@example.com", "Carol", "Shaw", "active", false),
            (Guid.Parse($"0{random[1..]}"), null, null, null, "deleted", false),
            (Guid.Parse($"f{random[1..]}"), null, null, null, "deleted", false),
        ]);
        page.GetProperty("items")[0].EnumerateObject().Select(field => field.Name)
            .ShouldBe(["id", "email", "firstName", "lastName", "status", "isSystemAdmin", "membershipCount", "createdAt"], ignoreOrder: true);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(50);
        page.GetProperty("total").GetInt32().ShouldBe(5);
    }

    [Fact]
    public async Task ListUsers_ALaterPage_ReturnsTheRestAndTheTotal()
    {
        await UseADatabaseOfItsOwnAsync();
        var admin = await _catalog.AddSystemAdminAsync(email: "admin@example.com");
        await _catalog.AddUserAsync(email: "bea@example.com");
        var last = await _catalog.AddUserAsync(email: "cem@example.com");

        var page = await _api.CreateClient(admin, secondFactor: true).GetFromJsonAsync<JsonElement>("/v1/platform/users?page=2&pageSize=2", Cancellation);

        Users(page).ShouldBe([(await _catalog.UserIdOfAsync(last), "cem@example.com", null, null, "active", false)]);
        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("total").GetInt32().ShouldBe(3);
    }

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    public async Task ListUsers_PagingOutsideItsRange_IsABadRequest(string query)
    {
        var admin = await _catalog.AddSystemAdminAsync();

        var response = await _api.CreateClient(admin, secondFactor: true).GetAsync($"/v1/platform/users?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("{0}@MATH.org", "Ada")]
    [InlineData("grace{0}", "Grace")]
    [InlineData("LOVELACE{0}", "Ada")]
    public async Task ListUsers_ASearch_FindsTheUsersWhoseEmailOrNameContainsItInAnyCase(string q, string found)
    {
        await _catalog.AddUserAsync(email: $"{_marker}@math.org", firstName: $"Ada{_marker}", lastName: $"Lovelace{_marker}");
        await _catalog.AddUserAsync(email: $"{_marker}@navy.mil", firstName: $"Grace{_marker}", lastName: $"Hopper{_marker}");

        var page = await (await AdminAsync()).GetFromJsonAsync<JsonElement>(
            $"/v1/platform/users?q={Uri.EscapeDataString(string.Format(CultureInfo.InvariantCulture, q, _marker))}", Cancellation);

        Users(page).Select(user => user.FirstName).ShouldBe([$"{found}{_marker}"]);
        page.GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("Per%c", "Per%cent")]
    [InlineData("Under_s", "Under_score")]
    [InlineData(@"Back\s", @"Back\slash")]
    public async Task ListUsers_ASearchWithAWildcard_MatchesTheCharacterItself(string q, string found)
    {
        await AddUsersAsync("Per%cent", "Perxcent", "Under_score", "Underxscore", @"Back\slash", "Backslash");

        var page = await (await AdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={Uri.EscapeDataString($"{_marker} {q}")}", Cancellation);

        Users(page).Select(user => user.LastName).ShouldBe([$"{_marker} {found}"]);
    }

    // A deleted user has no name or email address left to search for, so the test reads a database of its own. The admin who asks is
    // an active user too.
    [Theory]
    [InlineData("active", 2)]
    [InlineData("suspended", 1)]
    [InlineData("deleted", 1)]
    public async Task ListUsers_AStatusFilter_ReturnsTheUsersInThatStatusOnly(string status, int count)
    {
        await UseADatabaseOfItsOwnAsync();
        await _catalog.AddUserAsync(email: "active@example.com");
        var suspended = await _catalog.AddUserAsync(email: "suspended@example.com");
        await _catalog.SuspendUserAsync(suspended);
        var deleted = await _catalog.AddUserAsync(email: "deleted@example.com");
        await _catalog.DeleteUserAsync(deleted);
        await _catalog.SuspendUserAsync(deleted);

        var page = await (await AdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?status={status}", Cancellation);

        Users(page).Select(user => user.Status).ShouldBe(Enumerable.Repeat(status, count));
        page.GetProperty("total").GetInt32().ShouldBe(count);
    }

    // A user without a last name comes last in both directions, as a user without an email address does.
    [Theory]
    [InlineData("email", "al, bea, cy")]
    [InlineData("-email", "cy, bea, al")]
    [InlineData("lastName", "bea, al, cy")]
    [InlineData("-lastName", "al, bea, cy")]
    public async Task ListUsers_ASortField_OrdersByItInItsDirection(string sort, string emails)
    {
        await _catalog.AddUserAsync(email: $"bea{_marker}@t.org", lastName: "Young");
        await _catalog.AddUserAsync(email: $"al{_marker}@t.org", lastName: "Zed");
        await _catalog.AddUserAsync(email: $"cy{_marker}@t.org");

        var page = await (await AdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}&sort={sort}", Cancellation);

        EmailsOf(page).ShouldBe(emails);
    }

    [Theory]
    [InlineData("email", "a, b, <none>")]
    [InlineData("-email", "b, a, <none>")]
    public async Task ListUsers_SortedByEmail_PutsUsersWithoutOneLastInBothDirections(string sort, string emails)
    {
        await _catalog.AddUserAsync(email: $"b{_marker}@t.org", firstName: _marker);
        await _catalog.AddUserAsync(firstName: _marker);
        await _catalog.AddUserAsync(email: $"a{_marker}@t.org", firstName: _marker);

        var page = await (await AdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}&sort={sort}", Cancellation);

        EmailsOf(page).ShouldBe(emails);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("-email")]
    [InlineData("lastName")]
    [InlineData("-lastName")]
    public async Task ListUsers_TwoUsersWithTheSameSortValue_KeepTheirOrderAcrossPages(string sort)
    {
        var random = Guid.NewGuid().ToString();
        await _catalog.AddUserAsync(Guid.Parse($"f{random[1..]}"), email: $"same{_marker}@t.org", lastName: "Same");
        await _catalog.AddUserAsync(Guid.Parse($"0{random[1..]}"), email: $"same{_marker}@t.org", lastName: "Same");
        var admin = await AdminAsync();

        var first = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}&sort={sort}&pageSize=1&page=1", Cancellation);
        var second = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}&sort={sort}&pageSize=1&page=2", Cancellation);

        Users(first).Concat(Users(second)).Select(user => user.Id).ShouldBe([Guid.Parse($"0{random[1..]}"), Guid.Parse($"f{random[1..]}")]);
    }

    [Theory]
    [InlineData("sort=age", "paging.sort_invalid")]
    [InlineData("sort=Email", "paging.sort_invalid")]
    [InlineData("sort=firstName", "paging.sort_invalid")]
    [InlineData("status=Active", "paging.filter_invalid")]
    [InlineData("status=banned", "paging.filter_invalid")]
    [InlineData("q=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "paging.q_invalid")]
    public async Task ListUsers_AnUnknownSortOrFilterValueOrALongSearch_IsRejected(string query, string code)
    {
        var response = await (await AdminAsync()).GetAsync($"/v1/platform/users?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe(code);
    }

    // The users list shows how many organisations each user is in, and when they joined the catalog.
    [Fact]
    public async Task ListUsers_EachUser_ReturnsTheirMembershipCountAndWhenTheyWereCreated()
    {
        var created = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero);
        var user = await _catalog.AddUserAsync(email: $"{_marker}@example.com", createdAt: created);
        await _catalog.AddMemberAsync((await _catalog.AddTenantAsync()).Id, user);
        await _catalog.AddMemberAsync((await _catalog.AddTenantAsync()).Id, user);

        var item = (await (await AdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}", Cancellation)).GetProperty("items")[0];

        (item.GetProperty("membershipCount").GetInt32(), item.GetProperty("createdAt").GetDateTimeOffset()).ShouldBe((2, created));
    }

    // AcceptInvitation adds a membership and counts it on the user, a new one included, who is created at the module's clock.
    [Fact]
    public async Task ListUsers_AfterAUserJoinsTwoTenants_CountsBothMemberships()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await _api.DisposeAsync();
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: new FakeTimeProvider(now));
        var email = $"{_marker}@example.com";
        var invitee = $"user_{Guid.NewGuid():N}";
        _api.Identity.AddAccount(invitee, email);
        var first = await _catalog.AddTenantAsync();
        var second = await _catalog.AddTenantAsync();
        await _catalog.AddInvitationAsync(first.Id, email, secret: "first-secret", expiresAt: now.AddDays(7));
        await _catalog.AddInvitationAsync(second.Id, email, secret: "second-secret", expiresAt: now.AddDays(7));

        (await _api.WaitingForMessagesAsync(() => AcceptAsync(invitee, Catalog.InvitationCodeOf(first.Id, "first-secret")))).EnsureSuccessStatusCode();
        (await _api.WaitingForMessagesAsync(() => AcceptAsync(invitee, Catalog.InvitationCodeOf(second.Id, "second-secret")))).EnsureSuccessStatusCode();

        var item = (await (await AdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}", Cancellation)).GetProperty("items")[0];
        (item.GetProperty("membershipCount").GetInt32(), item.GetProperty("createdAt").GetDateTimeOffset()).ShouldBe((2, now));
        (await _catalog.StoredMembershipCountOfAsync(invitee)).ShouldBe(await _catalog.CountedMembershipsOfAsync(invitee));
    }

    // RemoveMember takes one membership away and counts it down on the user.
    [Fact]
    public async Task ListUsers_AfterAMemberIsRemovedFromOneOfTwoTenants_CountsOne()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var user = await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(email: $"{_marker}@example.com"));
        await _catalog.AddMemberAsync((await _catalog.AddTenantAsync()).Id, user);
        var id = await _catalog.UserIdOfAsync(user);

        (await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).RemoveMemberAsync(tenant.Id, id))).EnsureSuccessStatusCode();

        var item = (await (await AdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}", Cancellation)).GetProperty("items")[0];
        item.GetProperty("membershipCount").GetInt32().ShouldBe(1);
        (await _catalog.StoredMembershipCountOfAsync(user)).ShouldBe(await _catalog.CountedMembershipsOfAsync(user));
    }

    // A deletion takes the user out of every tenant first (RemoveUserMembership), and each step counts one down.
    [Fact]
    public async Task ListUsers_AfterTheUsersDeletion_CountsNone()
    {
        await UseADatabaseOfItsOwnAsync();
        var user = await _catalog.AddUserAsync(email: $"{_marker}@example.com");
        await _catalog.AddMemberAsync((await _catalog.AddTenantAsync()).Id, user);
        await _catalog.AddMemberAsync((await _catalog.AddTenantAsync()).Id, user);
        var admin = await AdminAsync();
        var id = await _catalog.UserIdOfAsync(user);

        (await _api.WaitingForMessagesAsync(() => admin.DeleteAsync($"/v1/platform/users/{id}", Cancellation))).EnsureSuccessStatusCode();

        var found = await admin.GetFromJsonAsync<JsonElement>("/v1/platform/users?status=deleted", Cancellation);
        found.GetProperty("items").EnumerateArray().Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("membershipCount").GetInt32()))
            .ShouldBe([(id, 0)]);
        (await _catalog.StoredMembershipCountOfAsync(user)).ShouldBe(0);
    }

    // The page is one query: the count and the creation time are on the user row, so a longer page sends no more commands (K5).
    [Fact]
    public async Task ListUsers_ALongerPage_SendsNoMoreSqlCommands()
    {
        using var commands = new SqlCommands();
        await AddUsersAsync("Ball", "Cole", "Dunn");
        var admin = await _catalog.AddSystemAdminAsync();

        var one = await commands.CountAsync(
            () => _api.CreateClient(admin, secondFactor: true), client => client.GetAsync($"/v1/platform/users?q={_marker}&pageSize=1", Cancellation));
        var three = await commands.CountAsync(
            () => _api.CreateClient(admin, secondFactor: true), client => client.GetAsync($"/v1/platform/users?q={_marker}&pageSize=3", Cancellation));

        three.ShouldBe(one);
        one.ShouldBeGreaterThan(0);
    }

    private Task<HttpResponseMessage> AcceptAsync(string userId, string code) =>
        _api.CreateClient(userId).PostAsJsonAsync("/v1/me/invitations/accept", new { code, firstName = "Ada", lastName = "Lovelace" }, Cancellation);

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    // A database where no other test adds a user, for a test that reads the whole list or a status.
    private async Task UseADatabaseOfItsOwnAsync()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        _catalog = new Catalog(database, name);
        await _api.DisposeAsync();
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, name));
    }

    // The part of each email address before the test's marker; <none> for a user without one.
    private string EmailsOf(JsonElement page) =>
        string.Join(", ", Users(page).Select(user => user.Email?.Split(_marker)[0] ?? "<none>"));

    // Users whose last names carry the test's marker, so that a search for the marker finds them alone.
    private async Task AddUsersAsync(params string[] lastNames)
    {
        foreach (var lastName in lastNames)
        {
            await _catalog.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", lastName: $"{_marker} {lastName}");
        }
    }

    private static List<(Guid Id, string? Email, string? FirstName, string? LastName, string Status, bool IsSystemAdmin)> Users(JsonElement page) =>
    [
        .. page.GetProperty("items").EnumerateArray().Select(item => (
            item.GetProperty("id").GetGuid(),
            item.GetProperty("email").GetString(),
            item.GetProperty("firstName").GetString(),
            item.GetProperty("lastName").GetString(),
            item.GetProperty("status").GetString()!,
            item.GetProperty("isSystemAdmin").GetBoolean())),
    ];
}
