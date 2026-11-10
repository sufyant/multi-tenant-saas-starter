using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Platform.Users;

// GET /v1/platform/users: every user, for the system admin, by email address, then id. Users belong to no tenant: a test that
// reads the whole list, or a status, has a database of its own, where no other test adds a user; any other test searches for a marker
// of its own.
public sealed class ListUsersTests(Database database) : IAsyncLifetime
{
    private readonly string _marker = Guid.NewGuid().ToString("N")[..12];

    private TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

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
        var carol = await _testData.AddUserAsync(email: TestEmails.Of("carol@example.com"), name: "Carol Shaw");
        var bob = await _testData.AddSystemAdminAsync(email: TestEmails.Of("bob@example.com"));
        var alice = await _testData.AddUserAsync(email: TestEmails.Of("alice@example.com"), name: "Alice Ball");
        await _testData.SuspendUserAsync(alice);
        var deletedLater = await _testData.AddUserAsync(Guid.Parse($"f{random[1..]}"), email: TestEmails.Of("dave@example.com"));
        await _testData.DeleteUserAsync(deletedLater);
        await _testData.SuspendUserAsync(deletedLater);
        var deletedEarlier = await _testData.AddUserAsync(Guid.Parse($"0{random[1..]}"), email: TestEmails.Of("erin@example.com"));
        await _testData.DeleteUserAsync(deletedEarlier);

        var response = await _api.CreateClient(bob, secondFactor: true).GetAsync("/v1/platform/users", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Users(page).ShouldBe(
        [
            (await _testData.UserIdOfAsync(alice), TestEmails.Of("alice@example.com"), "Alice Ball", "suspended", false),
            (await _testData.UserIdOfAsync(bob), TestEmails.Of("bob@example.com"), null, "active", true),
            (await _testData.UserIdOfAsync(carol), TestEmails.Of("carol@example.com"), "Carol Shaw", "active", false),
            (Guid.Parse($"0{random[1..]}"), null, null, "deleted", false),
            (Guid.Parse($"f{random[1..]}"), null, null, "deleted", false),
        ]);
        page.GetProperty("items")[0].EnumerateObject().Select(field => field.Name)
            .ShouldBe(
                ["id", "email", "name", "status", "isSystemAdmin", "isRootSystemAdmin", "systemAdminGrantedAt", "membershipCount", "createdAt"],
                ignoreOrder: true);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(50);
        page.GetProperty("total").GetInt32().ShouldBe(5);
        page.EnumerateObject().Select(field => field.Name).ShouldBe(["items", "page", "pageSize", "total", "statusCounts"], ignoreOrder: true);
        page.GetProperty("statusCounts").ToString().ShouldBe("""{"active":2,"suspended":1,"deleted":2}""");
    }

    // The tabs of the Users list count each status, the totals of the whole list (the maintainer's decision of 2026-11-10, which replaces
    // the one of 2026-11-08). Neither the search nor the status filter changes them; the total counts what both keep. The counts read the
    // whole list, so the test has a database of its own.
    [Fact]
    public async Task ListUsers_ASearchAndAStatusFilter_CountsEveryUserInEachStatusAndTotalsTheFilter()
    {
        await UseADatabaseOfItsOwnAsync();
        await AddUsersAsync("Ball", "Cole", "Dunn");
        var suspended = await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", name: $"{_marker} Fox");
        await _testData.SuspendUserAsync(suspended);
        await _testData.SuspendUserAsync(await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", name: "Another Person"));

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}&status=suspended", Cancellation);

        Users(page).Select(user => user.Name).ShouldBe([$"{_marker} Fox"]);
        page.GetProperty("total").GetInt32().ShouldBe(1);
        page.GetProperty("statusCounts").ToString().ShouldBe("""{"active":4,"suspended":2,"deleted":0}""");
    }

    // A search that finds one user and the isSystemAdmin filter leave every count at its total. The three active users are the two
    // added and the system admin who reads the list.
    [Theory]
    [InlineData("q=Ball", 1)]
    [InlineData("isSystemAdmin=true", 1)]
    public async Task ListUsers_ASearchOrAFilterThatKeepsOneUser_LeavesEveryCountAtItsTotal(string filter, int total)
    {
        await UseADatabaseOfItsOwnAsync();
        await AddUsersAsync("Ball", "Cole");
        await _testData.SuspendUserAsync(await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com"));
        await _testData.DeleteUserAsync(await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com"));

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?{filter}", Cancellation);

        page.GetProperty("total").GetInt32().ShouldBe(total);
        page.GetProperty("statusCounts").ToString().ShouldBe("""{"active":3,"suspended":1,"deleted":1}""");
    }

    // The status filter takes several values. A deleted user keeps no address or name to search for, so the test searches the active
    // and the suspended ones; the whole list's counts are in ListUsers_UsersInEveryStatus_ReturnsEachByEmailThenId.
    [Fact]
    public async Task ListUsers_SeveralStatuses_ReturnsTheUsersInAnyOfThem()
    {
        await _testData.AddUserAsync(email: $"active.{_marker}@example.com");
        await _testData.SuspendUserAsync(await _testData.AddUserAsync(email: $"suspended.{_marker}@example.com"));

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}&status=suspended&status=active", Cancellation);

        Users(page).Select(user => user.Status).ShouldBe(["active", "suspended"]);
        page.GetProperty("total").GetInt32().ShouldBe(2);
    }

    // K5: a list with a status filter sends as many commands as the list without one.
    [Fact]
    public async Task ListUsers_AStatusFilter_SendsNoMoreSqlCommands()
    {
        using var commands = new SqlCommands();
        await AddUsersAsync("Gray");
        var admin = await _testData.AddSystemAdminAsync();

        var all = await commands.CountAsync(
            () => _api.CreateClient(admin, secondFactor: true), client => client.GetAsync($"/v1/platform/users?q={_marker}", Cancellation));
        var filtered = await commands.CountAsync(
            () => _api.CreateClient(admin, secondFactor: true), client => client.GetAsync($"/v1/platform/users?q={_marker}&status=active&status=suspended", Cancellation));

        filtered.ShouldBe(all);
    }

    [Fact]
    public async Task ListUsers_ALaterPage_ReturnsTheRestAndTheTotal()
    {
        await UseADatabaseOfItsOwnAsync();
        var admin = await _testData.AddSystemAdminAsync(email: TestEmails.Of("admin@example.com"));
        await _testData.AddUserAsync(email: TestEmails.Of("bea@example.com"));
        var last = await _testData.AddUserAsync(email: TestEmails.Of("cem@example.com"));

        var page = await _api.CreateClient(admin, secondFactor: true).GetFromJsonAsync<JsonElement>("/v1/platform/users?page=2&pageSize=2", Cancellation);

        Users(page).ShouldBe([(await _testData.UserIdOfAsync(last), TestEmails.Of("cem@example.com"), null, "active", false)]);
        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("total").GetInt32().ShouldBe(3);
    }

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    public async Task ListUsers_PagingOutsideItsRange_IsABadRequest(string query)
    {
        var admin = await _testData.AddSystemAdminAsync();

        var response = await _api.CreateClient(admin, secondFactor: true).GetAsync($"/v1/platform/users?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("{0}@MATH.org", "Ada Lovelace")]
    [InlineData("grace hopper{0}", "Grace Hopper")]
    [InlineData("LOVELACE{0}", "Ada Lovelace")]
    public async Task ListUsers_ASearch_FindsTheUsersWhoseEmailOrNameContainsItInAnyCase(string q, string found)
    {
        await _testData.AddUserAsync(email: $"{_marker}@math.org", name: $"Ada Lovelace{_marker}");
        await _testData.AddUserAsync(email: $"{_marker}@navy.mil", name: $"Grace Hopper{_marker}");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>(
            $"/v1/platform/users?q={Uri.EscapeDataString(string.Format(CultureInfo.InvariantCulture, q, _marker))}", Cancellation);

        Users(page).Select(user => user.Name).ShouldBe([$"{found}{_marker}"]);
        page.GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("Per%c", "Per%cent")]
    [InlineData("Under_s", "Under_score")]
    [InlineData(@"Back\s", @"Back\slash")]
    public async Task ListUsers_ASearchWithAWildcard_MatchesTheCharacterItself(string q, string found)
    {
        await AddUsersAsync("Per%cent", "Perxcent", "Under_score", "Underxscore", @"Back\slash", "Backslash");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={Uri.EscapeDataString($"{_marker} {q}")}", Cancellation);

        Users(page).Select(user => user.Name).ShouldBe([$"{_marker} {found}"]);
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
        await _testData.AddUserAsync(email: TestEmails.Of("active@example.com"));
        var suspended = await _testData.AddUserAsync(email: TestEmails.Of("suspended@example.com"));
        await _testData.SuspendUserAsync(suspended);
        var deleted = await _testData.AddUserAsync(email: TestEmails.Of("deleted@example.com"));
        await _testData.DeleteUserAsync(deleted);
        await _testData.SuspendUserAsync(deleted);

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?status={status}", Cancellation);

        Users(page).Select(user => user.Status).ShouldBe(Enumerable.Repeat(status, count));
        page.GetProperty("total").GetInt32().ShouldBe(count);
    }

    // The system admins page (decision of 2026-10-24): the filter takes the value of the field isSystemAdmin. The admin who asks is one.
    [Theory]
    [InlineData("true", 2)]
    [InlineData("false", 1)]
    public async Task ListUsers_AnIsSystemAdminFilter_ReturnsTheUsersWithThatValueOnly(string value, int count)
    {
        await UseADatabaseOfItsOwnAsync();
        await _testData.AddUserAsync(email: TestEmails.Of("user@example.com"));
        await _testData.AddSystemAdminAsync(email: TestEmails.Of("admin@example.com"));

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?isSystemAdmin={value}", Cancellation);

        Users(page).Select(user => user.IsSystemAdmin).ShouldAllBe(isSystemAdmin => isSystemAdmin == bool.Parse(value));
        page.GetProperty("total").GetInt32().ShouldBe(count);
    }

    // The System admins "Add" dialog finds a person by the whole address: ali@ is found, and kali@, which contains it, is not.
    [Fact]
    public async Task ListUsers_AnEmailFilter_ReturnsTheUserWithExactlyThatAddress()
    {
        var ali = await _testData.AddUserAsync(email: $"ali{_marker}@northwind.example");
        await _testData.AddUserAsync(email: $"kali{_marker}@northwind.example");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>(
            $"/v1/platform/users?email={Uri.EscapeDataString($"ali{_marker}@northwind.example")}", Cancellation);

        Users(page).Select(user => user.Id).ShouldBe([await _testData.UserIdOfAsync(ali)]);
        page.GetProperty("total").GetInt32().ShouldBe(1);
    }

    // The filter takes the address's one form, as every address that enters the system does: trimmed, in lower case.
    [Fact]
    public async Task ListUsers_AnEmailFilterInAnotherCaseWithSpaces_ReturnsTheUserWithThatAddress()
    {
        var ali = await _testData.AddUserAsync(email: $"ali{_marker}@northwind.example");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>(
            $"/v1/platform/users?email={Uri.EscapeDataString($"  ALI{_marker}@Northwind.Example ")}", Cancellation);

        Users(page).Select(user => user.Id).ShouldBe([await _testData.UserIdOfAsync(ali)]);
    }

    [Fact]
    public async Task ListUsers_AnEmailFilterThatNoUserHas_ReturnsNone()
    {
        await _testData.AddUserAsync(email: $"ali{_marker}@northwind.example");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>(
            $"/v1/platform/users?email={Uri.EscapeDataString($"ali{_marker}@southwind.example")}", Cancellation);

        Users(page).ShouldBeEmpty();
        page.GetProperty("total").GetInt32().ShouldBe(0);
    }

    // Two live users never keep one address (A7, the maintainer's decision of 2026-11-04), so the filter answers the one user with it; a
    // deleted user keeps no address (A8) and never matches.
    [Fact]
    public async Task ListUsers_AnEmailFilter_ReturnsTheOneUserWithTheAddress()
    {
        var user = await _testData.AddUserAsync(email: $"ali{_marker}@northwind.example");
        await _testData.DeleteUserAsync(await _testData.AddUserAsync(email: $"ali-old{_marker}@northwind.example"));

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>(
            $"/v1/platform/users?email={Uri.EscapeDataString($"ali{_marker}@northwind.example")}", Cancellation);

        Users(page).Select(user => user.Id).ShouldBe([await _testData.UserIdOfAsync(user)]);
    }

    // An address is checked as the other email inputs are (CreateTenant, InviteMember, InviteOwner).
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@northwind.example")]
    [InlineData("ali@")]
    public async Task ListUsers_AnEmailFilterThatIsNoAddress_IsAValidationFailure(string email)
    {
        var response = await (await SystemAdminAsync()).GetAsync($"/v1/platform/users?email={Uri.EscapeDataString(email)}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("code").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors").GetProperty("email").EnumerateArray().Select(code => code.GetString()).ShouldBe(["email_invalid"]);
    }

    // An address longer than a column holds (EmailAddress.MaxLength) is refused as the other email inputs refuse it.
    [Fact]
    public async Task ListUsers_AnEmailFilterLongerThanAnAddress_IsAValidationFailure()
    {
        var email = $"{new string('a', 310)}@northwind.example";

        var response = await (await SystemAdminAsync()).GetAsync($"/v1/platform/users?email={Uri.EscapeDataString(email)}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("code").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors").GetProperty("email").EnumerateArray().Select(code => code.GetString()).ShouldBe(["too_long"]);
    }

    // An empty or blank filter is no filter, as an empty status, sort or q is (K5 in api-catalog).
    [Theory]
    [InlineData("")]
    [InlineData("%20%20")]
    public async Task ListUsers_AnEmptyOrBlankEmailFilter_IsNoFilter(string email)
    {
        await _testData.AddUserAsync(email: $"ali{_marker}@northwind.example");
        await _testData.AddUserAsync(email: $"kali{_marker}@northwind.example");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}&email={email}", Cancellation);

        page.GetProperty("total").GetInt32().ShouldBe(2);
    }

    // The "Add" dialog's search combines with the other filters: the user with the address is listed only when they are a system admin.
    [Fact]
    public async Task ListUsers_AnEmailFilterWithIsSystemAdmin_ReturnsTheSystemAdminWithThatAddressOnly()
    {
        var admin = await _testData.AddSystemAdminAsync(email: $"ali{_marker}@northwind.example");
        await _testData.AddUserAsync(email: $"kali{_marker}@northwind.example");
        var client = await SystemAdminAsync();

        var ofTheAdmin = await client.GetFromJsonAsync<JsonElement>(
            $"/v1/platform/users?isSystemAdmin=true&email={Uri.EscapeDataString($"ali{_marker}@northwind.example")}", Cancellation);
        var ofTheUser = await client.GetFromJsonAsync<JsonElement>(
            $"/v1/platform/users?isSystemAdmin=true&email={Uri.EscapeDataString($"kali{_marker}@northwind.example")}", Cancellation);

        Users(ofTheAdmin).Select(user => user.Id).ShouldBe([await _testData.UserIdOfAsync(admin)]);
        Users(ofTheUser).ShouldBeEmpty();
    }

    // A system admin's grant keeps when it was granted, and the list answers it; a user who is no system admin has none.
    [Fact]
    public async Task ListUsers_ASystemAdminAndAUser_AnswersWhenTheSystemAdminWasGranted()
    {
        var admin = await _testData.AddSystemAdminAsync(email: $"a{_marker}@example.com");
        await _testData.AddUserAsync(email: $"b{_marker}@example.com");
        var granted = await database.ScalarAsync<DateTime>(
            $"SELECT granted_at FROM control_plane.system_admins WHERE user_id = '{await _testData.UserIdOfAsync(admin)}'");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}", Cancellation);

        var items = page.GetProperty("items").EnumerateArray().ToList();
        items[0].GetProperty("systemAdminGrantedAt").GetDateTimeOffset().UtcDateTime.ShouldBe(granted);
        items[1].GetProperty("systemAdminGrantedAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // A root system admin who has never entered the platform has no grant yet, so the system admins page does not list them.
    [Fact]
    public async Task ListUsers_ARootWhoNeverEnteredThePlatform_IsNoSystemAdminInTheFilter()
    {
        var root = RootEmail();
        await _testData.AddUserAsync(email: root.ToLowerInvariant());
        await using var api = WithRoots(root);

        var page = await api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true)
            .GetFromJsonAsync<JsonElement>($"/v1/platform/users?isSystemAdmin=true&q={Uri.EscapeDataString(root.ToLowerInvariant())}", Cancellation);

        page.GetProperty("total").GetInt32().ShouldBe(0);
    }

    // A user without a name comes last in both directions, as a user without an email address does.
    [Theory]
    [InlineData("email", "al, bea, cy")]
    [InlineData("-email", "cy, bea, al")]
    [InlineData("name", "bea, al, cy")]
    [InlineData("-name", "al, bea, cy")]
    public async Task ListUsers_ASortField_OrdersByItInItsDirection(string sort, string emails)
    {
        await _testData.AddUserAsync(email: $"bea{_marker}@t.org", name: "Young");
        await _testData.AddUserAsync(email: $"al{_marker}@t.org", name: "Zed");
        await _testData.AddUserAsync(email: $"cy{_marker}@t.org");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}&sort={sort}", Cancellation);

        EmailsOf(page).ShouldBe(emails);
    }

    [Theory]
    [InlineData("email", "a, b, <none>")]
    [InlineData("-email", "b, a, <none>")]
    public async Task ListUsers_SortedByEmail_PutsUsersWithoutOneLastInBothDirections(string sort, string emails)
    {
        await _testData.AddUserAsync(email: $"b{_marker}@t.org", name: _marker);
        await _testData.AddUserAsync(name: _marker);
        await _testData.AddUserAsync(email: $"a{_marker}@t.org", name: _marker);

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}&sort={sort}", Cancellation);

        EmailsOf(page).ShouldBe(emails);
    }

    // Two live users never keep one address (A7), so only the name can tie.
    [Theory]
    [InlineData("name")]
    [InlineData("-name")]
    public async Task ListUsers_TwoUsersWithTheSameSortValue_KeepTheirOrderAcrossPages(string sort)
    {
        var random = Guid.NewGuid().ToString();
        await _testData.AddUserAsync(Guid.Parse($"f{random[1..]}"), email: $"same1{_marker}@t.org", name: "Same");
        await _testData.AddUserAsync(Guid.Parse($"0{random[1..]}"), email: $"same2{_marker}@t.org", name: "Same");
        var admin = await SystemAdminAsync();

        var first = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}&sort={sort}&pageSize=1&page=1", Cancellation);
        var second = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}&sort={sort}&pageSize=1&page=2", Cancellation);

        Users(first).Concat(Users(second)).Select(user => user.Id).ShouldBe([Guid.Parse($"0{random[1..]}"), Guid.Parse($"f{random[1..]}")]);
    }

    [Theory]
    [InlineData("sort=age", "paging.sort_invalid")]
    [InlineData("sort=Email", "paging.sort_invalid")]
    [InlineData("sort=lastName", "paging.sort_invalid")]
    [InlineData("status=Active", "paging.filter_invalid")]
    [InlineData("status=banned", "paging.filter_invalid")]
    [InlineData("isSystemAdmin=yes", "request.invalid")]
    [InlineData("isSystemAdmin=", "request.invalid")]
    [InlineData("q=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "paging.q_invalid")]
    public async Task ListUsers_AnUnknownSortOrFilterValueOrALongSearch_IsRejected(string query, string code)
    {
        var response = await (await SystemAdminAsync()).GetAsync($"/v1/platform/users?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe(code);
    }

    // The users list shows how many tenants each user is in, and when they became a user.
    [Fact]
    public async Task ListUsers_EachUser_ReturnsTheirMembershipCountAndWhenTheyWereCreated()
    {
        var created = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero);
        var user = await _testData.AddUserAsync(email: $"{_marker}@example.com", createdAt: created);
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, user);
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, user);

        var item = (await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}", Cancellation)).GetProperty("items")[0];

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
        var (firstSecret, secondSecret) = (Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        var invitee = $"user_{Guid.NewGuid():N}";
        _api.Identity.AddAccount(invitee, email);
        var first = await _testData.AddTenantAsync();
        var second = await _testData.AddTenantAsync();
        await _testData.AddInvitationAsync(first.Id, email, secret: firstSecret, expiresAt: now.AddDays(7));
        await _testData.AddInvitationAsync(second.Id, email, secret: secondSecret, expiresAt: now.AddDays(7));

        (await _api.WaitingForMessagesAsync(() => AcceptAsync(invitee, TestData.InvitationCodeOf(first.Id, firstSecret)))).EnsureSuccessStatusCode();
        (await _api.WaitingForMessagesAsync(() => AcceptAsync(invitee, TestData.InvitationCodeOf(second.Id, secondSecret)))).EnsureSuccessStatusCode();

        var item = (await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}", Cancellation)).GetProperty("items")[0];
        (item.GetProperty("membershipCount").GetInt32(), item.GetProperty("createdAt").GetDateTimeOffset()).ShouldBe((2, now));
        (await _testData.StoredMembershipCountOfAsync(invitee)).ShouldBe(await _testData.CountedMembershipsOfAsync(invitee));
    }

    // RemoveMember takes one membership away and counts it down on the user.
    [Fact]
    public async Task ListUsers_AfterAMemberIsRemovedFromOneOfTwoTenants_CountsOne()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var user = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: $"{_marker}@example.com"));
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, user);
        var id = await _testData.UserIdOfAsync(user);

        (await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).RemoveMemberAsync(tenant.Id, id))).EnsureSuccessStatusCode();

        var item = (await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={_marker}", Cancellation)).GetProperty("items")[0];
        item.GetProperty("membershipCount").GetInt32().ShouldBe(1);
        (await _testData.StoredMembershipCountOfAsync(user)).ShouldBe(await _testData.CountedMembershipsOfAsync(user));
    }

    // A deletion takes the user out of every tenant first (RemoveUserMembershipCommand), and each step counts one down.
    [Fact]
    public async Task ListUsers_AfterTheUsersDeletion_CountsNone()
    {
        await UseADatabaseOfItsOwnAsync();
        var user = await _testData.AddUserAsync(email: $"{_marker}@example.com");
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, user);
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, user);
        var admin = await SystemAdminAsync();
        var id = await _testData.UserIdOfAsync(user);

        (await _api.WaitingForMessagesAsync(() => admin.DeleteAsync($"/v1/platform/users/{id}", Cancellation))).EnsureSuccessStatusCode();

        var found = await admin.GetFromJsonAsync<JsonElement>("/v1/platform/users?status=deleted", Cancellation);
        found.GetProperty("items").EnumerateArray().Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("membershipCount").GetInt32()))
            .ShouldBe([(id, 0)]);
        (await _testData.StoredMembershipCountOfAsync(user)).ShouldBe(0);
    }

    // The page is one query: the count and the creation time are on the user row, so a longer page sends no more commands (K5).
    [Fact]
    public async Task ListUsers_ALongerPage_SendsNoMoreSqlCommands()
    {
        using var commands = new SqlCommands();
        await AddUsersAsync("Ball", "Cole", "Dunn");
        var admin = await _testData.AddSystemAdminAsync();

        var one = await commands.CountAsync(
            () => _api.CreateClient(admin, secondFactor: true), client => client.GetAsync($"/v1/platform/users?q={_marker}&pageSize=1", Cancellation));
        var three = await commands.CountAsync(
            () => _api.CreateClient(admin, secondFactor: true), client => client.GetAsync($"/v1/platform/users?q={_marker}&pageSize=3", Cancellation));

        three.ShouldBe(one);
        one.ShouldBeGreaterThan(0);
    }

    // A listed address of a user who is no system admin makes no root: root describes a system admin.
    [Fact]
    public async Task ListUsers_AListedUserWhoIsNoSystemAdmin_IsNoRoot()
    {
        var root = RootEmail();
        var user = await _testData.AddUserAsync(email: root.ToLowerInvariant());
        await using var api = WithRoots(root);

        var page = await api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true)
            .GetFromJsonAsync<JsonElement>($"/v1/platform/users?q={Uri.EscapeDataString(root.ToLowerInvariant())}", Cancellation);

        var found = page.GetProperty("items").EnumerateArray().Single();
        (found.GetProperty("id").GetGuid(), found.GetProperty("isSystemAdmin").GetBoolean(), found.GetProperty("isRootSystemAdmin").GetBoolean())
            .ShouldBe((await _testData.UserIdOfAsync(user), false, false));
    }

    private static List<(Guid Id, string? Email, string? Name, string Status, bool IsSystemAdmin)> Users(JsonElement page) =>
    [
        .. page.GetProperty("items").EnumerateArray().Select(item => (
            item.GetProperty("id").GetGuid(),
            item.GetProperty("email").GetString(),
            item.GetProperty("name").GetString(),
            item.GetProperty("status").GetString()!,
            item.GetProperty("isSystemAdmin").GetBoolean())),
    ];

    // An address in mixed case, so the list and the stored address meet only in their one form.
    private static string RootEmail() => $"Root.{Guid.NewGuid():N}@Example.com";

    private ApiFactory WithRoots(params string[] roots) =>
        new(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: roots.Select((root, index) => KeyValuePair.Create($"ControlPlane:SystemAdmins:RootEmails:{index}", (string?)root)).ToDictionary());

    private Task<HttpResponseMessage> AcceptAsync(string userId, string code) =>
        _api.CreateClient(userId).PostAsJsonAsync("/v1/me/invitations/accept", new { code, name = "Ada Lovelace" }, Cancellation);

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    // A database where no other test adds a user, for a test that reads the whole list or a status.
    private async Task UseADatabaseOfItsOwnAsync()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        _testData = new TestData(database, name);
        await _api.DisposeAsync();
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, name));
    }

    // The part of each email address before the test's marker; <none> for a user without one.
    private string EmailsOf(JsonElement page) =>
        string.Join(", ", Users(page).Select(user => user.Email?.Split(_marker)[0] ?? "<none>"));

    // Users whose names carry the test's marker, so that a search for the marker finds them alone.
    private async Task AddUsersAsync(params string[] names)
    {
        foreach (var name in names)
        {
            await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", name: $"{_marker} {name}");
        }
    }
}
